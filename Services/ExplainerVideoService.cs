using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PostFlow.Configuration;
using PostFlow.Exceptions;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Models.DTOs.Responses;
using PostFlow.Services.Interfaces;

namespace PostFlow.Services
{
    public class ExplainerVideoService : IExplainerVideoService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly AISettings _aiSettings;
        private readonly ExplainerVideoJobManager _jobManager;
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly ILogger<ExplainerVideoService> _logger;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public ExplainerVideoService(
            IHttpClientFactory httpClientFactory,
            IOptions<AISettings> aiSettings,
            ExplainerVideoJobManager jobManager,
            IBackgroundJobClient backgroundJobClient,
            ILogger<ExplainerVideoService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _aiSettings = aiSettings.Value;
            _jobManager = jobManager;
            _backgroundJobClient = backgroundJobClient;
            _logger = logger;
        }

        public async Task<ExplainerResearchResponse> ResearchTopicAsync(string userId, ExplainerResearchRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Topic))
            {
                throw new BadRequestException("Chủ đề nghiên cứu không được để trống.");
            }

            var prompt = BuildResearchPrompt(request);

            try
            {
                var candidateText = await CallAIAsync(prompt);
                if (string.IsNullOrWhiteSpace(candidateText))
                {
                    throw new BadRequestException("AI không phản hồi kết quả nghiên cứu hợp lệ.");
                }

                var cleanJson = ExtractJsonContent(candidateText);
                var result = JsonSerializer.Deserialize<ExplainerResearchResponse>(cleanJson, JsonOptions);
                if (result == null || string.IsNullOrWhiteSpace(result.CoreDefinition))
                {
                    throw new BadRequestException("Không thể phân tích dữ liệu nghiên cứu từ AI.");
                }

                result.Topic = request.Topic;
                result.TargetAudience = request.TargetAudience ?? "Khán giả đại chúng";
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
            var prompt = BuildStoryboardPrompt(request);

            try
            {
                var candidateText = await CallAIAsync(prompt);
                if (string.IsNullOrWhiteSpace(candidateText))
                {
                    throw new BadRequestException("AI không phản hồi kịch bản phân cảnh hợp lệ.");
                }

                var cleanJson = ExtractJsonContent(candidateText);
                var result = JsonSerializer.Deserialize<ExplainerStoryboardResponse>(cleanJson, JsonOptions);
                if (result == null || result.Scenes == null || result.Scenes.Count == 0)
                {
                    throw new BadRequestException("Kịch bản phân cảnh rỗng.");
                }

                // Chuẩn hóa và làm giàu dữ liệu Visual Motion cho từng phân cảnh
                var category = request.Category ?? "tech";
                foreach (var scene in result.Scenes)
                {
                    scene.Category = category;
                    scene.Type = NormalizeSceneType(scene.Type);
                    if (string.IsNullOrWhiteSpace(scene.VisualIcon))
                    {
                        scene.VisualIcon = DeduceVisualIcon(scene.Type, scene.Title, category);
                    }
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
            if (string.IsNullOrWhiteSpace(request.Topic))
            {
                throw new BadRequestException("Chủ đề nghiên cứu không được để trống.");
            }

            var jobId = _jobManager.Create(userId, "Research");
            var requestJson = JsonSerializer.Serialize(request);

            _backgroundJobClient.Enqueue<ExplainerVideoBackgroundJobs>(
                job => job.ResearchTopicJobAsync(jobId, userId, requestJson)
            );

            return jobId;
        }

        public string CreateStoryboardJob(string userId, ExplainerRenderRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Topic))
            {
                throw new BadRequestException("Chủ đề video không được để trống.");
            }

            var jobId = _jobManager.Create(userId, "Storyboard");
            var requestJson = JsonSerializer.Serialize(request);

            _backgroundJobClient.Enqueue<ExplainerVideoBackgroundJobs>(
                job => job.GenerateStoryboardJobAsync(jobId, userId, requestJson)
            );

            return jobId;
        }

        public string CreateRenderJob(string userId, ExplainerRenderRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Topic))
            {
                throw new BadRequestException("Chủ đề video không được để trống.");
            }

            var jobId = _jobManager.Create(userId, "Render");
            var requestJson = JsonSerializer.Serialize(request);

            _backgroundJobClient.Enqueue<ExplainerVideoBackgroundJobs>(
                job => job.RenderExplainerVideoAsync(jobId, userId, requestJson)
            );

            return jobId;
        }

        public ExplainerJobStatusResponse? GetJobStatus(string jobId, string userId)
        {
            return _jobManager.Get(jobId, userId);
        }

        private async Task<string> CallAIAsync(string promptText)
        {
            var geminiKey = !string.IsNullOrWhiteSpace(_aiSettings?.GeminiApiKey)
                ? _aiSettings.GeminiApiKey
                : Environment.GetEnvironmentVariable("GEMINI_API_KEY");

            if (string.IsNullOrWhiteSpace(geminiKey))
            {
                throw new BadRequestException("Chưa cấu hình GeminiApiKey trong AISettings hoặc biến môi trường GEMINI_API_KEY.");
            }

            var errors = new List<string>();

            // Chỉ dùng AI trực tiếp từ Google Gemini API
            var geminiModels = new[]
            {
                "gemini-2.0-flash",
                "gemini-1.5-flash",
                "gemini-flash-latest",
                "gemini-2.5-flash",
                "gemini-1.5-pro",
                "gemini-3.5-flash",
                "gemini-3.8-flash"
            };

            foreach (var model in geminiModels)
            {
                try
                {
                    var geminiResult = await TryCallGeminiModelAsync(geminiKey, model, promptText, errors);
                    if (!string.IsNullOrWhiteSpace(geminiResult))
                    {
                        return geminiResult;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Thử nghiệm model Gemini {Model} thất bại: {Msg}", model, ex.Message);
                    errors.Add($"Gemini {model}: {ex.Message}");
                }
            }

            var combinedErrors = errors.Count > 0 ? string.Join("; ", errors) : "Không nhận được phản hồi từ các mô hình Gemini AI.";
            throw new BadRequestException($"Gemini AI không phản hồi kết quả hợp lệ. Chi tiết: {combinedErrors}");
        }

        private async Task<string?> TryCallGeminiModelAsync(string apiKey, string model, string promptText, List<string> errors)
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromMinutes(3); // Tăng timeout lên 3 phút theo yêu cầu

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";

            // Thử lần 1: với thinking_budget = 0 (tắt reasoning nếu model hỗ trợ để tối ưu tốc độ)
            var requestBodyWithThinking = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[] { new { text = promptText } }
                    }
                },
                generationConfig = new
                {
                    response_mime_type = "application/json",
                    temperature = 0.6,
                    thinking_config = new
                    {
                        thinking_budget = 0
                    }
                }
            };

            var (success, result, error) = await ExecuteGeminiRequestAsync(client, url, apiKey, requestBodyWithThinking);
            if (success && !string.IsNullOrWhiteSpace(result))
            {
                return result;
            }

            // Nếu gặp lỗi HTTP 400 do model không hỗ trợ tham số thinking_config, tự động gọi lại theo cấu hình standard
            if (error != null && (error.Contains("thinking", StringComparison.OrdinalIgnoreCase) || error.Contains("Unknown field", StringComparison.OrdinalIgnoreCase) || error.Contains("INVALID_ARGUMENT", StringComparison.OrdinalIgnoreCase)))
            {
                var requestBodyStandard = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[] { new { text = promptText } }
                        }
                    },
                    generationConfig = new
                    {
                        response_mime_type = "application/json",
                        temperature = 0.6
                    }
                };

                var (success2, result2, error2) = await ExecuteGeminiRequestAsync(client, url, apiKey, requestBodyStandard);
                if (success2 && !string.IsNullOrWhiteSpace(result2))
                {
                    return result2;
                }
                error = error2 ?? error;
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                errors.Add($"Gemini {model}: {error}");
            }

            return null;
        }

        private async Task<(bool success, string? result, string? error)> ExecuteGeminiRequestAsync(HttpClient client, string url, string apiKey, object payload)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("x-goog-api-key", apiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Gemini API lỗi HTTP {Code}: {Error}", (int)response.StatusCode, err);
                return (false, null, $"HTTP {(int)response.StatusCode}");
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);

            if (!doc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            {
                return (false, null, "Không tìm thấy candidates");
            }

            var contentParts = candidates[0].GetProperty("content").GetProperty("parts");
            string? textResult = null;
            foreach (var part in contentParts.EnumerateArray())
            {
                if (part.TryGetProperty("thought", out var isThought) && isThought.GetBoolean())
                {
                    continue;
                }
                if (part.TryGetProperty("text", out var textProp))
                {
                    textResult = textProp.GetString();
                }
            }

            if (string.IsNullOrWhiteSpace(textResult) && contentParts.GetArrayLength() > 0)
            {
                textResult = contentParts[0].GetProperty("text").GetString();
            }

            return (!string.IsNullOrWhiteSpace(textResult), textResult, null);
        }

        private static string BuildResearchPrompt(ExplainerResearchRequest req)
        {
            var isFinance = string.Equals(req.Category, "finance", StringComparison.OrdinalIgnoreCase);
            var isGeneral = string.Equals(req.Category, "general", StringComparison.OrdinalIgnoreCase);

            var roleDescription = isFinance
                ? "Bạn là Chuyên gia Nghiên cứu Tài chính, Đầu tư Thông minh & Kinh tế học hàng đầu thế giới (Phong cách Ray Dalio, Graham Stephan, Bloomberg Quicktake)."
                : isGeneral
                ? "Bạn là Chuyên gia Phổ biến Kiến thức Khoa học & Đời sống hàng đầu thế giới (Phong cách Kurzgesagt, Vox, Ted-Ed)."
                : "Bạn là Giám đốc Nghiên cứu Nội dung Giáo dục & Giải thích Công nghệ hàng đầu thế giới (Phong cách 3Blue1Brown, Fireship).";

            var defaultAudience = isFinance ? "Nhà đầu tư cá nhân / Người đi làm" : isGeneral ? "Đại chúng / Người tò mò học hỏi" : "Lập trình viên / Người yêu công nghệ";
            var defaultTone = isFinance ? "Thực tế, dẫn chứng số liệu, dễ hiểu" : isGeneral ? "Sinh động, lôi cuốn, kích thích tư duy" : "Cuốn hút, súc tích, thực tế";

            return $@"{roleDescription}
Hãy nghiên cứu chuyên sâu về chủ đề: ""{req.Topic}"".
Đối tượng khán giả: {req.TargetAudience ?? defaultAudience}
Văn phong: {req.Tone ?? defaultTone}

BẮT BUỘC TRẢ VỀ JSON THUẦN TÚY (application/json) theo định dạng sau:
{{
  ""coreDefinition"": ""Định nghĩa súc tích, dễ hiểu nhất về khái niệm này (khoảng 2 câu)"",
  ""keyFacts"": [
    ""Dữ kiện/Số liệu thực tế 1"",
    ""Dữ kiện/Số liệu thực tế 2"",
    ""Dữ kiện/Số liệu thực tế 3""
  ],
  ""commonMisconceptions"": [
    ""Hiểu lầm phổ biến 1 mà 90% mọi người mắc phải"",
    ""Hiểu lầm phổ biến 2""
  ],
  ""recommendedHooks"": [
    ""Câu hook 1 (Gợi mở vấn đề bất ngờ trong 3s)"",
    ""Câu hook 2 (Câu hỏi khiêu khích)"",
    ""Câu hook 3 (Tuyên bố gây sốc về dữ liệu)""
  ],
  ""suggestedChapters"": [
    {{
      ""title"": ""Chương 1: Mở đầu & Đặt vấn đề"",
      ""description"": ""Nêu bật nỗi đau hoặc câu hỏi lớn"",
      ""layoutType"": ""hero""
    }},
    {{
      ""title"": ""Chương 2: Cơ chế hoạt động cốt lõi"",
      ""description"": ""Giải thích cách vận hành qua sơ đồ luồng"",
      ""layoutType"": ""flowchart""
    }},
    {{
      ""title"": ""Chương 3: So sánh trực quan"",
      ""description"": ""Đặt lên bàn cân với giải pháp thay thế"",
      ""layoutType"": ""card-comparison""
    }},
    {{
      ""title"": ""Chương 4: Điểm nhấn số liệu & Kết luận"",
      ""description"": ""Đưa ra con số ấn tượng và bài học rút ra"",
      ""layoutType"": ""metric-highlight""
    }}
  ]
}}";
        }

        private static string NormalizeSceneType(string? rawType)
        {
            var t = (rawType ?? string.Empty).ToLower().Trim().Replace("-", "").Replace("_", "");
            if (t.Contains("flow") || t.Contains("chart") || t.Contains("arch") || t.Contains("diagram") || t.Contains("process") || t.Contains("pipeline") || t.Contains("workflow"))
                return "flowchart";
            if (t.Contains("compar") || t.Contains("card") || t.Contains("versus") || t.Contains("vs") || t.Contains("table"))
                return "card-comparison";
            if (t.Contains("metric") || t.Contains("stat") || t.Contains("number") || t.Contains("highlight") || t.Contains("benchmark"))
                return "metric-highlight";
            if (t.Contains("code") || t.Contains("snippet") || t.Contains("terminal") || t.Contains("syntax") || t.Contains("script"))
                return "code-snippet";
            if (t.Contains("takeaway") || t.Contains("summary") || t.Contains("conclusion"))
                return "key-takeaway";
            return "hero";
        }

        private static string DeduceVisualIcon(string sceneType, string? title, string? category = "tech")
        {
            var text = (title ?? string.Empty).ToLower();
            var isFinance = string.Equals(category, "finance", StringComparison.OrdinalIgnoreCase);

            if (isFinance)
            {
                if (text.Contains("tiền") || text.Contains("thu nhập") || text.Contains("đô") || text.Contains("usd") || text.Contains("giàu")) return "dollar";
                if (text.Contains("đầu tư") || text.Contains("tăng trưởng") || text.Contains("lợi nhuận") || text.Contains("lãi")) return "trending-up";
                if (text.Contains("tiết kiệm") || text.Contains("chi phí") || text.Contains("ví") || text.Contains("ngân sách")) return "wallet";
                if (text.Contains("lạm phát") || text.Contains("thị trường") || text.Contains("thống kê") || text.Contains("chu kỳ")) return "barchart";
                if (text.Contains("rủi ro") || text.Contains("bảo vệ") || text.Contains("an toàn") || text.Contains("bảo hiểm")) return "shield";
                if (text.Contains("mục tiêu") || text.Contains("chiến lược")) return "target";
                if (text.Contains("ngân hàng") || text.Contains("thể chế")) return "landmark";
                return sceneType switch
                {
                    "flowchart" => "trending-up",
                    "card-comparison" => "piechart",
                    "metric-highlight" => "coins",
                    _ => "dollar"
                };
            }

            if (text.Contains("database") || text.Contains("db") || text.Contains("lưu trữ") || text.Contains("sql")) return "database";
            if (text.Contains("bảo mật") || text.Contains("security") || text.Contains("gateway") || text.Contains("shield") || text.Contains("auth")) return "shield";
            if (text.Contains("server") || text.Contains("máy chủ") || text.Contains("backend")) return "server";
            if (text.Contains("cloud") || text.Contains("đám mây") || text.Contains("docker") || text.Contains("k8s") || text.Contains("container")) return "boxes";
            if (text.Contains("tốc độ") || text.Contains("hiệu năng") || text.Contains("fast") || text.Contains("speed")) return "zap";
            if (text.Contains("mạng") || text.Contains("network") || text.Contains("kết nối")) return "network";
            if (text.Contains("code") || text.Contains("lập trình") || text.Contains("terminal")) return "terminal";

            return sceneType switch
            {
                "flowchart" => "network",
                "card-comparison" => "layers",
                "metric-highlight" => "zap",
                "code-snippet" => "terminal",
                _ => "sparkles"
            };
        }

        private static string BuildStoryboardPrompt(ExplainerRenderRequest req)
        {
            var isFinance = string.Equals(req.Category, "finance", StringComparison.OrdinalIgnoreCase);
            var isGeneral = string.Equals(req.Category, "general", StringComparison.OrdinalIgnoreCase);

            var directorRole = isFinance
                ? "Bạn là Giám đốc Sáng tạo & Đồ họa Chuyển động (Creative Visual Director Agent) hàng đầu thế giới cho video giải thích Tài chính, Đầu tư Thông minh & Kinh tế học Remotion (Phong cách Ray Dalio, Bloomberg Quicktake, Graham Stephan)."
                : isGeneral
                ? "Bạn là Giám đốc Sáng tạo & Đồ họa Chuyển động (Creative Visual Director Agent) hàng đầu thế giới cho video giải thích Khoa học & Đời sống Remotion (Phong cách Kurzgesagt, Vox, Ted-Ed)."
                : "Bạn là Giám đốc Sáng tạo & Đồ họa Chuyển động (Creative Visual Director Agent) hàng đầu thế giới cho video giải thích công nghệ Remotion (Phong cách 3Blue1Brown, Fireship, Kurzgesagt).";

            var allowedIcons = isFinance
                ? @"""dollar"", ""trending-up"", ""coins"", ""wallet"", ""barchart"", ""piechart"", ""percent"", ""landmark"", ""target"", ""shield"", ""zap"", ""sparkles"""
                : @"""server"", ""database"", ""cloud"", ""cpu"", ""boxes"", ""shield"", ""terminal"", ""network"", ""laptop"", ""zap"", ""sparkles"", ""activity"", ""layers""";

            var defaultScene2 = isFinance
                ? @"{
      ""id"": ""s2"",
      ""type"": ""flowchart"",
      ""visualIcon"": ""trending-up"",
      ""title"": ""Quy Trình Quản Lý & Tăng Trưởng Vốn"",
      ""subtitle"": ""Dòng tiền luân chuyển thông minh"",
      ""narration"": ""Lời thoại phân tích các bước vận hành dòng tiền"",
      ""durationInFrames"": 150,
      ""flowchartNodes"": [
        { ""id"": ""1"", ""label"": ""Thu Nhập"", ""sublabel"": ""Dòng tiền vào"", ""icon"": ""wallet"", ""status"": ""normal"" },
        { ""id"": ""2"", ""label"": ""Chi Phí Cố Định"", ""sublabel"": ""Quy tắc 50%"", ""icon"": ""creditcard"", ""status"": ""normal"" },
        { ""id"": ""3"", ""label"": ""Quỹ Đầu Tư"", ""sublabel"": ""Tích lũy tài sản"", ""icon"": ""trending-up"", ""status"": ""highlight"" },
        { ""id"": ""4"", ""label"": ""Tự Do Tài Chính"", ""sublabel"": ""Lợi nhuận kép"", ""icon"": ""coins"", ""status"": ""active"" }
      ]
    }"
                : @"{
      ""id"": ""s2"",
      ""type"": ""flowchart"",
      ""visualIcon"": ""network"",
      ""title"": ""Cơ Chế Hoạt Động Cốt Lõi"",
      ""subtitle"": ""Quy trình xử lý dữ liệu tự động"",
      ""narration"": ""Lời thoại giải thích luồng hoạt động từng bước"",
      ""durationInFrames"": 150,
      ""flowchartNodes"": [
        { ""id"": ""1"", ""label"": ""Client"", ""sublabel"": ""Gửi request"", ""icon"": ""laptop"", ""status"": ""normal"" },
        { ""id"": ""2"", ""label"": ""API Gateway"", ""sublabel"": ""Bảo mật & Điều phối"", ""icon"": ""shield"", ""status"": ""active"" },
        { ""id"": ""3"", ""label"": ""Core Engine"", ""sublabel"": ""Xử lý siêu tốc"", ""icon"": ""cpu"", ""status"": ""highlight"" },
        { ""id"": ""4"", ""label"": ""Database"", ""sublabel"": ""Lưu trữ an toàn"", ""icon"": ""database"", ""status"": ""normal"" }
      ]
    }";

            var defaultScene3 = isFinance
                ? @"{
      ""id"": ""s3"",
      ""type"": ""card-comparison"",
      ""visualIcon"": ""piechart"",
      ""title"": ""So Sánh Hiệu Quả Tài Chính"",
      ""subtitle"": ""Sự khác biệt sau 10 năm"",
      ""narration"": ""Lời thoại đối chiếu giữa giữ tiền mặt và đầu tư kỷ luật"",
      ""durationInFrames"": 150,
      ""comparisonCards"": [
        {
          ""name"": ""Giữ Tiền Mặt"",
          ""badge"": ""Truyền Thống"",
          ""icon"": ""wallet"",
          ""pros"": [""An toàn ngắn hạn""],
          ""cons"": [""Bị lạm phát bào mòn"", ""Mất giá theo thời gian""],
          ""isWinner"": false
        },
        {
          ""name"": ""Đầu Tư Kỷ Luật"",
          ""badge"": ""Đột Phá"",
          ""icon"": ""trending-up"",
          ""pros"": [""Hưởng trọn sức mạnh lãi kép"", ""Bảo vệ tài sản trước lạm phát"", ""Tự do tài chính sớm""],
          ""cons"": [],
          ""isWinner"": true
        }
      ]
    }"
                : @"{
      ""id"": ""s3"",
      ""type"": ""card-comparison"",
      ""visualIcon"": ""layers"",
      ""title"": ""So Sánh Hiệu Năng"",
      ""subtitle"": ""Sự khác biệt vượt trội"",
      ""narration"": ""Lời thoại đối chiếu giữa phương pháp cũ và kiến trúc mới"",
      ""durationInFrames"": 150,
      ""comparisonCards"": [
        {
          ""name"": ""Giải Pháp Cũ"",
          ""badge"": ""Truyền Thống"",
          ""icon"": ""server"",
          ""pros"": [""Dễ bắt đầu""],
          ""cons"": [""Độ trễ cao"", ""Tốn tài nguyên""],
          ""isWinner"": false
        },
        {
          ""name"": ""Giải Pháp Mới"",
          ""badge"": ""Đột Phá"",
          ""icon"": ""zap"",
          ""pros"": [""Nhanh gấp 10 lần"", ""Tự động mở rộng"", ""Tiết kiệm 80% chi phí""],
          ""cons"": [],
          ""isWinner"": true
        }
      ]
    }";

            var defaultScene4 = isFinance
                ? @"{
      ""id"": ""s4"",
      ""type"": ""metric-highlight"",
      ""visualIcon"": ""coins"",
      ""title"": ""Sức Mạnh Của Lãi Kép"",
      ""narration"": ""Lời thoại nhấn mạnh thành quả tăng trưởng tài sản"",
      ""durationInFrames"": 120,
      ""metric"": {
        ""value"": ""72"",
        ""prefix"": ""Quy tắc "",
        ""suffix"": """",
        ""label"": ""Số Năm Nhân Đôi Tài Sản"",
        ""description"": ""Lấy 72 chia cho lãi suất hàng năm sẽ biết chính xác thời gian tài sản của bạn tăng gấp đôi.""
      }
    }"
                : @"{
      ""id"": ""s4"",
      ""type"": ""metric-highlight"",
      ""visualIcon"": ""zap"",
      ""title"": ""Hiệu Quả Thực Tế Đột Phá"",
      ""narration"": ""Lời thoại nhấn mạnh thành quả và số liệu ấn tượng"",
      ""durationInFrames"": 120,
      ""metric"": {
        ""value"": ""10x"",
        ""prefix"": """",
        ""suffix"": """",
        ""label"": ""Tốc Độ Xử Lý Nhanh Hơn"",
        ""description"": ""Giảm thiểu tối đa độ trễ và tối ưu chi phí vận hành máy chủ.""
      }
    }";

            var defaultTags = isFinance
                ? @"[""TĂNG TRƯỞNG TÀI SẢN"", ""LÃI KÉP THÔNG MINH""]"
                : @"[""KIẾN TRÚC HIỆN ĐẠI"", ""TỐI ƯU HIỆU NĂNG""]";

            return $@"{directorRole}
Chủ đề: ""{req.Topic}""
Lĩnh vực: {req.Category ?? "tech"}
Tỷ lệ khung hình: {req.AspectRatio}
Thời lượng dự kiến: {req.TargetDuration ?? "30-45 giây"}

Hãy thiết kế kịch bản gồm 3 đến 5 phân cảnh (Scenes) phong cách Motion Graphics hiện đại, TUYỆT ĐỐI KHÔNG để khung hình trống hay chỉ có chữ thô sơ. BẮT BUỘC mỗi phân cảnh phải có biểu tượng trực quan động (visualIcon), thẻ tag động (tags) gồm 2 từ khóa nổi bật của chủ đề, và cấu trúc phân cảnh trực quan.
{(isFinance ? "CHÚ Ý: Với chủ đề Tài chính, TUYỆT ĐỐI KHÔNG dùng loại cảnh 'code-snippet'. Hãy dùng 'flowchart', 'card-comparison', 'metric-highlight', hoặc 'hero'." : "")}

Danh sách biểu tượng visualIcon hợp lệ:
- {allowedIcons}.

Các loại Scene Type chuẩn (viết thường):
1. ""hero"": Mở đầu hoặc kết luận cuốn hút với biểu tượng trung tâm lớn phát sáng, tiêu đề, phụ đề và mảng tags gồm 2 từ khóa ngắn gọn đại diện chủ đề.
2. ""flowchart"": Sơ đồ luồng xử lý hoặc chu trình gồm 3-4 node, mỗi node có icon riêng, kết nối bởi tia năng lượng chuyển động.
3. ""card-comparison"": So sánh 2 bên (Cách cũ vs Cách mới / Giữ tiền vs Đầu tư), có badge, icon, ưu điểm (pros), nhược điểm (cons), đánh dấu bên chiến thắng (isWinner: true).
4. ""metric-highlight"": Điểm nhấn số liệu đột phá kèm đồng hồ đo vòng tròn SVG phát sáng.
5. ""code-snippet"": Cửa sổ dòng lệnh Terminal (chỉ áp dụng cho chủ đề Công nghệ).

BẮT BUỘC TRẢ VỀ ĐÚNG ĐỊNH DẠNG JSON (application/json):
{{
  ""topic"": ""{req.Topic}"",
  ""title"": ""Tiêu đề video cuốn hút"",
  ""aspectRatio"": ""{req.AspectRatio}"",
  ""totalEstimatedSeconds"": 35,
  ""scenes"": [
    {{
      ""id"": ""s1"",
      ""type"": ""hero"",
      ""visualIcon"": ""{(isFinance ? "dollar" : "sparkles")}"",
      ""tags"": {defaultTags},
      ""title"": ""Tiêu đề phân cảnh mở đầu"",
      ""subtitle"": ""Mô tả ngắn gọn gợi mở vấn đề"",
      ""narration"": ""Lời thoại đọc dẫn dắt bằng tiếng Việt"",
      ""durationInFrames"": 120
    }},
    {defaultScene2},
    {defaultScene3},
    {defaultScene4}
  ]
}}";
        }

        private static string ExtractJsonContent(string text)
        {
            var trimmed = text.Trim();
            if (trimmed.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            {
                var startIndex = trimmed.IndexOf('\n') + 1;
                var endIndex = trimmed.LastIndexOf("```", StringComparison.OrdinalIgnoreCase);
                if (endIndex > startIndex)
                {
                    trimmed = trimmed.Substring(startIndex, endIndex - startIndex).Trim();
                }
            }
            else if (trimmed.StartsWith("```"))
            {
                var startIndex = trimmed.IndexOf('\n') + 1;
                var endIndex = trimmed.LastIndexOf("```", StringComparison.OrdinalIgnoreCase);
                if (endIndex > startIndex)
                {
                    trimmed = trimmed.Substring(startIndex, endIndex - startIndex).Trim();
                }
            }

            var first = trimmed.IndexOf('{');
            var last = trimmed.LastIndexOf('}');
            if (first >= 0 && last > first)
            {
                return trimmed.Substring(first, last - first + 1);
            }

            return trimmed;
        }
    }
}

