using System.Text.Json;
using PostFlow.Data;
using PostFlow.Models;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Services.Explainer;
using PostFlow.Services.Interfaces;

namespace PostFlow.Services
{
    /// <summary>
    /// Các tác vụ nền (Hangfire) của Explainer Video. Tên lớp, namespace và chữ ký phương thức public
    /// được giữ nguyên vì Hangfire lưu chúng trong hàng đợi.
    /// </summary>
    public class ExplainerVideoBackgroundJobs
    {
        private const int MinimumTotalFrames = 90;
        // Đệm 0.5 giây cuối để audio kết thúc tự nhiên, không bị cụt câu.
        private const double TailPaddingSeconds = 0.5;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly AppDbContext _context;
        private readonly IStorageService _storageService;
        private readonly ExplainerVideoJobManager _jobManager;
        private readonly IExplainerVideoService _explainerService;
        private readonly IEdgeTtsAligner _ttsAligner;
        private readonly IRemotionRenderer _renderer;
        private readonly ILogger<ExplainerVideoBackgroundJobs> _logger;

        public ExplainerVideoBackgroundJobs(
            AppDbContext context,
            IStorageService storageService,
            ExplainerVideoJobManager jobManager,
            IExplainerVideoService explainerService,
            IEdgeTtsAligner ttsAligner,
            IRemotionRenderer renderer,
            ILogger<ExplainerVideoBackgroundJobs> logger)
        {
            _context = context;
            _storageService = storageService;
            _jobManager = jobManager;
            _explainerService = explainerService;
            _ttsAligner = ttsAligner;
            _renderer = renderer;
            _logger = logger;
        }

        public Task ResearchTopicJobAsync(string jobId, string userId, string requestJson) =>
            RunAiJobAsync<ExplainerResearchRequest, Models.DTOs.Responses.ExplainerResearchResponse>(
                jobId, userId, requestJson,
                jobName: "nghiên cứu chủ đề",
                progressMessage: "Đang phân tích chuyên sâu chủ đề qua AI...",
                invalidRequestMessage: "Dữ liệu yêu cầu nghiên cứu không hợp lệ.",
                execute: _explainerService.ResearchTopicAsync,
                complete: _jobManager.CompleteResearch);

        public Task GenerateStoryboardJobAsync(string jobId, string userId, string requestJson) =>
            RunAiJobAsync<ExplainerRenderRequest, Models.DTOs.Responses.ExplainerStoryboardResponse>(
                jobId, userId, requestJson,
                jobName: "thiết kế phân cảnh",
                progressMessage: "Đang thiết kế kịch bản & phân cảnh chuyển động qua AI...",
                invalidRequestMessage: "Dữ liệu yêu cầu phân cảnh không hợp lệ.",
                execute: _explainerService.GenerateStoryboardAsync,
                complete: _jobManager.CompleteStoryboard);

        public async Task RenderExplainerVideoAsync(string jobId, string userId, string requestJson)
        {
            var request = Deserialize<ExplainerRenderRequest>(requestJson);
            if (request == null)
            {
                _jobManager.Failed(jobId, "Dữ liệu yêu cầu render không hợp lệ.");
                return;
            }

            var workDirectory = Path.Combine(Path.GetTempPath(), "postflow_explainer", jobId);
            Directory.CreateDirectory(workDirectory);

            try
            {
                var scenes = await PrepareScenesAsync(jobId, userId, request);
                var narration = await SynthesizeNarrationAsync(jobId, request, scenes, workDirectory);
                var timeline = BuildTimeline(jobId, scenes, narration);

                _jobManager.UpdateProgress(jobId, "PreparingProps", 65, "Đang khởi tạo tham số Remotion Engine...");
                var props = BuildRenderProps(jobId, request, scenes, narration, timeline);

                _jobManager.UpdateProgress(jobId, "RenderingVideo", 75, "Đang kết xuất video Remotion 1080p bằng Chromium...");
                var videoPath = Path.Combine(workDirectory, "final_explainer.mp4");
                await _renderer.RenderAsync(request.AspectRatio, props, workDirectory, videoPath);

                _jobManager.UpdateProgress(jobId, "Uploading", 90, "Đang tải video hoàn chỉnh lên lưu trữ...");
                var asset = await SaveVideoAssetAsync(userId, request, videoPath, timeline.TotalFrames);
                var downloadUrl = await _storageService.GeneratePresignedDownloadUrlAsync(asset.LocalFilePath);

                _jobManager.Completed(jobId, asset.Id, downloadUrl);
                _logger.LogInformation("Render Explainer Video thành công. JobId: {JobId}, VideoAssetId: {VideoId}", jobId, asset.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xử lý render Explainer Video JobId: {JobId}", jobId);
                _jobManager.Failed(jobId, ex.Message);
            }
            finally
            {
                CleanUp(jobId, workDirectory);
            }
        }

        // ───────────────────────── Các bước của pipeline render ─────────────────────────

        private async Task<List<ExplainerSceneDto>> PrepareScenesAsync(string jobId, string userId, ExplainerRenderRequest request)
        {
            _jobManager.UpdateProgress(jobId, "Processing", 15, "Đang thiết kế kịch bản & phân cảnh chuyển động...");

            var scenes = request.CustomScenes ?? new List<ExplainerSceneDto>();
            if (scenes.Count == 0)
            {
                var storyboard = await _explainerService.GenerateStoryboardAsync(userId, request);
                scenes = storyboard.Scenes;
            }

            if (scenes.Count == 0)
            {
                throw new InvalidOperationException("Không thể tạo phân cảnh kịch bản.");
            }

            ExplainerSceneNormalizer.ApplyDefaults(scenes, request.Category);
            return scenes;
        }

        private async Task<SceneNarration> SynthesizeNarrationAsync(
            string jobId, ExplainerRenderRequest request, List<ExplainerSceneDto> scenes, string workDirectory)
        {
            _jobManager.UpdateProgress(jobId, "GeneratingAudio", 35, "Đang sinh giọng đọc tiếng Việt & đồng bộ thời gian từng từ...");

            // Làm sạch từng scene riêng để số từ mỗi scene khớp với những gì TTS thực sự đọc.
            var sceneTexts = scenes.Select(scene => NarrationHelper.Clean(scene.Narration)).ToList();
            var fullText = string.Join(" ", sceneTexts.Where(text => text.Length > 0));
            if (string.IsNullOrWhiteSpace(fullText))
            {
                fullText = NarrationHelper.Clean(request.Topic);
            }
            fullText = NarrationHelper.EnsureTerminalPunctuation(fullText);

            var audio = await _ttsAligner.SynthesizeAsync(
                fullText, new VoiceOptions(request.Voice, request.Rate, request.Pitch), workDirectory);

            return new SceneNarration(audio, sceneTexts.Select(NarrationHelper.CountWords).ToList());
        }

        private SceneTimeline BuildTimeline(string jobId, List<ExplainerSceneDto> scenes, SceneNarration narration)
        {
            _jobManager.UpdateProgress(jobId, "Aligning", 50, "Đang phân bổ nhịp phân cảnh theo audio...");

            var audioFrames = (int)Math.Ceiling((narration.Audio.TotalDurationSeconds + TailPaddingSeconds) * RemotionDefaults.Fps);
            var totalFrames = Math.Max(MinimumTotalFrames, audioFrames);

            var durations = SceneTimingAllocator.Allocate(
                narration.SceneWordCounts,
                narration.Audio.Words.Select(word => word.StartMs).ToList(),
                totalFrames,
                RemotionDefaults.Fps);

            var chapters = new List<RemotionChapter>(scenes.Count);
            var startFrame = 0;
            for (var i = 0; i < scenes.Count; i++)
            {
                scenes[i].DurationInFrames = durations[i];
                chapters.Add(new RemotionChapter($"c_{i + 1}", scenes[i].Title, startFrame, durations[i]));
                startFrame += durations[i];
            }

            // Tổng thực tế có thể lớn hơn totalFrames nếu scene cuối phải nâng lên mức tối thiểu.
            return new SceneTimeline(chapters, startFrame);
        }

        private RemotionRenderProps BuildRenderProps(
            string jobId, ExplainerRenderRequest request, List<ExplainerSceneDto> scenes,
            SceneNarration narration, SceneTimeline timeline)
        {
            var audioPath = _renderer.StageAudio(jobId, narration.Audio.AudioPath);
            var subtitles = narration.Audio.Words
                .Select(word => new RemotionSubtitle(word.Text, word.StartMs, word.EndMs))
                .ToList();

            return new RemotionRenderProps(
                Topic: request.Topic,
                Category: request.Category ?? ExplainerCategories.Tech,
                AspectRatio: request.AspectRatio,
                DurationInFrames: timeline.TotalFrames,
                AudioPath: audioPath,
                Subtitles: subtitles,
                Chapters: timeline.Chapters,
                Scenes: scenes,
                Theme: RemotionTheme.ForCategory(request.Category));
        }

        private async Task<VideoAsset> SaveVideoAssetAsync(
            string userId, ExplainerRenderRequest request, string videoPath, int totalFrames)
        {
            var asset = new VideoAsset
            {
                UserId = userId,
                Title = !string.IsNullOrWhiteSpace(request.Title) ? request.Title : $"Explainer: {request.Topic}",
                Description = $"Video đồ họa chuyển động Remotion giải thích chủ đề: {request.Topic}",
                Status = "Ready",
                DurationSeconds = (int)Math.Round((double)totalFrames / RemotionDefaults.Fps)
            };

            await using (var stream = File.OpenRead(videoPath))
            {
                asset.LocalFilePath = await _storageService.SaveFileAsync(
                    stream, $"videos/{userId}", $"{Guid.NewGuid():N}.mp4", "video/mp4");
            }

            _context.VideoAssets.Add(asset);
            await _context.SaveChangesAsync();
            return asset;
        }

        private void CleanUp(string jobId, string workDirectory)
        {
            _renderer.ReleaseAudio(jobId);

            try
            {
                if (Directory.Exists(workDirectory)) Directory.Delete(workDirectory, recursive: true);
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "Không thể xóa thư mục tạm {WorkDirectory}", workDirectory);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Không có quyền xóa thư mục tạm {WorkDirectory}", workDirectory);
            }
        }

        // ───────────────────────── Tiện ích dùng chung ─────────────────────────

        /// <summary>Khung chung cho tác vụ gọi AI: parse request → chạy → ghi kết quả / lỗi vào job manager.</summary>
        private async Task RunAiJobAsync<TRequest, TResult>(
            string jobId, string userId, string requestJson,
            string jobName, string progressMessage, string invalidRequestMessage,
            Func<string, TRequest, Task<TResult>> execute,
            Action<string, TResult> complete) where TRequest : class
        {
            try
            {
                _logger.LogInformation("Bắt đầu tác vụ nền {JobName}. JobId: {JobId}, UserId: {UserId}", jobName, jobId, userId);
                _jobManager.UpdateProgress(jobId, "Processing", 25, progressMessage);

                var request = Deserialize<TRequest>(requestJson);
                if (request == null)
                {
                    _jobManager.Failed(jobId, invalidRequestMessage);
                    return;
                }

                complete(jobId, await execute(userId, request));
                _logger.LogInformation("Hoàn tất tác vụ nền {JobName}. JobId: {JobId}", jobName, jobId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi trong tác vụ nền {JobName}. JobId: {JobId}", jobName, jobId);
                _jobManager.Failed(jobId, ex.Message);
            }
        }

        private static T? Deserialize<T>(string json) where T : class =>
            JsonSerializer.Deserialize<T>(json, JsonOptions);

        private sealed record SceneNarration(NarrationAudio Audio, IReadOnlyList<int> SceneWordCounts);

        private sealed record SceneTimeline(IReadOnlyList<RemotionChapter> Chapters, int TotalFrames);
    }
}
