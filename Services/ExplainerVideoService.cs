using System.Linq.Expressions;
using System.Text.Json;
using Hangfire;
using PostFlow.Exceptions;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Models.DTOs.Responses;
using PostFlow.Services.Explainer;
using PostFlow.Services.Interfaces;

namespace PostFlow.Services
{
    public class ExplainerVideoService : IExplainerVideoService
    {
        private const string ResearchJobType = "Research";
        private const string StoryboardJobType = "Storyboard";
        private const string RenderJobType = "Render";

        private const string ResearchTopicRequiredMessage = "Chủ đề nghiên cứu không được để trống.";
        private const string VideoTopicRequiredMessage = "Chủ đề video không được để trống.";
        private const string DefaultTargetAudience = "Khán giả đại chúng";

        private const double RetryTemperature = 0.4;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly IGeminiClient _gemini;
        private readonly ExplainerVideoJobManager _jobManager;
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly ILogger<ExplainerVideoService> _logger;

        public ExplainerVideoService(
            IGeminiClient gemini,
            ExplainerVideoJobManager jobManager,
            IBackgroundJobClient backgroundJobClient,
            ILogger<ExplainerVideoService> logger)
        {
            _gemini = gemini;
            _jobManager = jobManager;
            _backgroundJobClient = backgroundJobClient;
            _logger = logger;
        }

        public async Task<ExplainerResearchResponse> ResearchTopicAsync(string userId, ExplainerResearchRequest request)
        {
            RequireTopic(request.Topic, ResearchTopicRequiredMessage);

            try
            {
                var result = await RequestValidatedJsonAsync<ExplainerResearchResponse>(
                    issues => ExplainerPromptBuilder.BuildResearchPrompt(request, issues),
                    GeminiSchemas.Research(),
                    ExplainerOutputValidator.ValidateResearch,
                    emptyResponseMessage: "AI không phản hồi kết quả nghiên cứu hợp lệ.");

                if (string.IsNullOrWhiteSpace(result.CoreDefinition))
                {
                    throw new BadRequestException("Không thể phân tích dữ liệu nghiên cứu từ AI.");
                }

                result.Topic = request.Topic;
                result.TargetAudience = request.TargetAudience ?? DefaultTargetAudience;
                return result;
            }
            catch (Exception ex) when (ex is not BadRequestException)
            {
                _logger.LogError(ex, "Lỗi gọi AI khi nghiên cứu chủ đề: {Topic}", request.Topic);
                throw new BadRequestException($"Lỗi kết nối AI: {ex.Message}");
            }
        }

        public async Task<ExplainerStoryboardResponse> GenerateStoryboardAsync(string userId, ExplainerRenderRequest request)
        {
            try
            {
                var result = await RequestValidatedJsonAsync<ExplainerStoryboardResponse>(
                    issues => ExplainerPromptBuilder.BuildStoryboardPrompt(request, issues),
                    GeminiSchemas.Storyboard(),
                    candidate => ExplainerOutputValidator.ValidateStoryboard(candidate, request),
                    emptyResponseMessage: "AI không phản hồi kịch bản phân cảnh hợp lệ.");

                if (result.Scenes == null || result.Scenes.Count == 0)
                {
                    throw new BadRequestException("Kịch bản phân cảnh rỗng.");
                }

                EnrichScenes(result.Scenes, request.Category);

                var downgraded = ExplainerSceneNormalizer.DowngradeIncompleteScenes(result.Scenes);
                if (downgraded > 0)
                {
                    _logger.LogWarning("Hạ {Count} cảnh thiếu dữ liệu bắt buộc về hero để không render khung trống.", downgraded);
                }
                result.Topic = request.Topic;
                result.AspectRatio = request.AspectRatio;
                return result;
            }
            catch (Exception ex) when (ex is not BadRequestException)
            {
                _logger.LogError(ex, "Lỗi sinh kịch bản phân cảnh cho: {Topic}", request.Topic);
                throw new BadRequestException($"Lỗi sinh kịch bản phân cảnh: {ex.Message}");
            }
        }

        public string CreateResearchJob(string userId, ExplainerResearchRequest request)
        {
            RequireTopic(request.Topic, ResearchTopicRequiredMessage);
            return EnqueueJob(userId, request, ResearchJobType,
                (jobId, requestJson) => job => job.ResearchTopicJobAsync(jobId, userId, requestJson));
        }

        public string CreateStoryboardJob(string userId, ExplainerRenderRequest request)
        {
            RequireTopic(request.Topic, VideoTopicRequiredMessage);
            return EnqueueJob(userId, request, StoryboardJobType,
                (jobId, requestJson) => job => job.GenerateStoryboardJobAsync(jobId, userId, requestJson));
        }

        public string CreateRenderJob(string userId, ExplainerRenderRequest request)
        {
            RequireTopic(request.Topic, VideoTopicRequiredMessage);
            return EnqueueJob(userId, request, RenderJobType,
                (jobId, requestJson) => job => job.RenderExplainerVideoAsync(jobId, userId, requestJson));
        }

        public ExplainerJobStatusResponse? GetJobStatus(string jobId, string userId) =>
            _jobManager.Get(jobId, userId);

        private string EnqueueJob<TRequest>(
            string userId,
            TRequest request,
            string jobType,
            Func<string, string, Expression<Func<ExplainerVideoBackgroundJobs, Task>>> buildJob)
        {
            var jobId = _jobManager.Create(userId, jobType);
            var requestJson = JsonSerializer.Serialize(request);

            _backgroundJobClient.Enqueue(buildJob(jobId, requestJson));
            return jobId;
        }

        /// <summary>
        /// Gọi AI, kiểm tra chất lượng đầu ra; nếu có lỗi thì sinh lại đúng một lần kèm danh sách lỗi.
        /// Sau lần thử lại, trả về bản ít lỗi hơn thay vì từ chối hẳn (bản đó vẫn dùng được khi không có lỗi nghiêm trọng).
        /// </summary>
        private async Task<T> RequestValidatedJsonAsync<T>(
            Func<IReadOnlyList<string>?, ExplainerPrompt> buildPrompt,
            object schema,
            Func<T, IReadOnlyList<string>> validate,
            string emptyResponseMessage) where T : class
        {
            var first = await TryRequestAsync(buildPrompt(null), schema, validate, temperature: null);
            if (first.Result != null && first.Issues.Count == 0) return first.Result;

            var retryIssues = first.Issues.Count > 0 ? first.Issues : new[] { "Đầu ra không phải JSON hợp lệ theo schema." };
            _logger.LogWarning("Đầu ra AI chưa đạt ({Count} lỗi), sinh lại: {Issues}", retryIssues.Count, string.Join(" | ", retryIssues));

            Attempt<T> second;
            try
            {
                second = await TryRequestAsync(buildPrompt(retryIssues), schema, validate, temperature: RetryTemperature);
            }
            catch (Exception ex) when (first.Result != null)
            {
                _logger.LogWarning(ex, "Lần sinh lại thất bại, dùng bản đầu tiên.");
                return first.Result;
            }

            var best = PickBest(first, second) ?? throw new BadRequestException(emptyResponseMessage);
            if (best.Issues.Count > 0)
            {
                _logger.LogWarning("Dùng bản còn {Count} cảnh báo chất lượng: {Issues}", best.Issues.Count, string.Join(" | ", best.Issues));
            }
            return best.Result!;
        }

        private static Attempt<T>? PickBest<T>(Attempt<T> first, Attempt<T> second) where T : class
        {
            if (second.Result == null) return first.Result == null ? null : first;
            if (first.Result == null) return second;
            return second.Issues.Count <= first.Issues.Count ? second : first;
        }

        private async Task<Attempt<T>> TryRequestAsync<T>(
            ExplainerPrompt prompt, object schema, Func<T, IReadOnlyList<string>> validate, double? temperature) where T : class
        {
            var responseText = await _gemini.GenerateJsonAsync(
                prompt.User,
                new GeminiRequestOptions(prompt.System, schema, temperature));

            if (string.IsNullOrWhiteSpace(responseText))
            {
                return new Attempt<T>(null, Array.Empty<string>());
            }

            T? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<T>(AiJsonExtractor.Extract(responseText), JsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Không đọc được JSON từ AI.");
                return new Attempt<T>(null, Array.Empty<string>());
            }

            return parsed == null
                ? new Attempt<T>(null, Array.Empty<string>())
                : new Attempt<T>(parsed, validate(parsed));
        }

        private sealed record Attempt<T>(T? Result, IReadOnlyList<string> Issues) where T : class;

        /// <summary>Chuẩn hóa loại scene, gán lĩnh vực và icon trực quan cho từng phân cảnh AI sinh ra.</summary>
        private static void EnrichScenes(IEnumerable<ExplainerSceneDto> scenes, string? requestedCategory)
        {
            var category = requestedCategory ?? ExplainerCategories.Tech;
            foreach (var scene in scenes)
            {
                scene.Category = category;
                scene.Type = ExplainerSceneNormalizer.NormalizeSceneType(scene.Type);
                if (string.IsNullOrWhiteSpace(scene.VisualIcon))
                {
                    scene.VisualIcon = ExplainerSceneNormalizer.DeduceVisualIcon(scene.Type, scene.Title, category);
                }
            }
        }

        private static void RequireTopic(string? topic, string message)
        {
            if (string.IsNullOrWhiteSpace(topic))
            {
                throw new BadRequestException(message);
            }
        }
    }
}
