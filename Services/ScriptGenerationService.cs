using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PostFlow.Configuration;
using PostFlow.Data;
using PostFlow.Exceptions;
using PostFlow.Models;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Models.DTOs.Responses;
using PostFlow.Services.Interfaces;

namespace PostFlow.Services
{
    public class ScriptGenerationService : IScriptGenerationService
    {
        private readonly AppDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly AISettings _aiSettings;
        private readonly ILogger<ScriptGenerationService> _logger;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public ScriptGenerationService(
            AppDbContext context,
            IHttpClientFactory httpClientFactory,
            IOptions<AISettings> aiSettings,
            ILogger<ScriptGenerationService> logger)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _aiSettings = aiSettings.Value;
            _logger = logger;
        }

        /// <summary>
        /// Sinh kịch bản video ngắn tự động bằng Google Gemini AI
        /// </summary>
        public async Task<ScriptDto> GenerateScriptAsync(string userId, GenerateScriptRequest request)
        {
            ContentDNA? dna = null;
            if (!string.IsNullOrEmpty(request.ContentDnaId))
            {
                dna = await _context.ContentDNAs
                    .FirstOrDefaultAsync(d => d.Id == request.ContentDnaId && d.UserId == userId);
            }

            var tone = !string.IsNullOrWhiteSpace(request.Tone)
                ? request.Tone
                : (!string.IsNullOrWhiteSpace(dna?.VoiceProfile) ? dna.VoiceProfile : "Năng động, cuốn hút, giàu thông tin thực tế");

            var targetDuration = !string.IsNullOrWhiteSpace(request.TargetDuration)
                ? request.TargetDuration
                : "30-45 giây";

            // Sinh kịch bản trực tiếp qua Google Gemini API
            var geminiKey = !string.IsNullOrWhiteSpace(_aiSettings?.GeminiApiKey)
                ? _aiSettings.GeminiApiKey
                : Environment.GetEnvironmentVariable("GEMINI_API_KEY");

            if (string.IsNullOrWhiteSpace(geminiKey))
            {
                _logger.LogError("Thiếu cấu hình GeminiApiKey trong AISettings hoặc biến môi trường GEMINI_API_KEY.");
                throw new BadRequestException("Hệ thống chưa được cấu hình Gemini API Key. Vui lòng kiểm tra lại cài đặt.");
            }

            try
            {
                var geminiScript = await CallGeminiForScriptAsync(geminiKey, request.Topic, tone, targetDuration, request.CustomPrompt);
                if (geminiScript != null && geminiScript.Scenes.Count > 0)
                {
                    await LogApiUsageAsync(userId, "Gemini", 1, 0.005m);
                    return geminiScript;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Gọi Google Gemini API thất bại: {Msg}", ex.Message);
                throw new BadRequestException($"Lỗi khi kết nối với Gemini AI: {ex.Message}");
            }

            _logger.LogError("Không thể sinh kịch bản từ Gemini AI (kết quả trả về rỗng hoặc không đúng định dạng).");
            throw new BadRequestException("Dịch vụ Gemini AI không phản hồi kịch bản hợp lệ. Vui lòng thử lại với chủ đề khác.");
        }

        private async Task<ScriptDto?> CallGeminiForScriptAsync(
            string apiKey, 
            string topic, 
            string tone, 
            string targetDuration,
            string? customPrompt)
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(60);

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-3.5-flash:generateContent";
            var promptText = BuildScriptPrompt(topic, tone, targetDuration, customPrompt);

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = promptText }
                        }
                    }
                },
                generationConfig = new
                {
                    response_mime_type = "application/json",
                    temperature = 0.7
                }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("x-goog-api-key", apiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            using var response = await client.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Gemini API error {StatusCode}: {Error}", response.StatusCode, errContent);
                return null;
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);

            if (!doc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            {
                return null;
            }

            var candidateText = candidates[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            if (string.IsNullOrWhiteSpace(candidateText)) return null;

            var cleanJson = ExtractJsonContent(candidateText);
            if (string.IsNullOrWhiteSpace(cleanJson)) return null;

            var script = JsonSerializer.Deserialize<ScriptDto>(cleanJson, JsonOptions);

            if (script != null && script.Scenes.Count > 0)
            {
                script.EstimatedDuration = script.Scenes.Sum(s => s.Duration);
                script.ModelUsed = "google/gemini-3.5-flash";
                script.ProviderUsed = "Google Gemini Direct";
            }

            return script;
        }

        private static string BuildScriptPrompt(string topic, string tone, string targetDuration, string? customPrompt)
        {
            return $@"Bạn là chuyên gia sáng tạo kịch bản video ngắn (TikTok, YouTube Shorts, Reels) hàng đầu.
Chủ đề: {topic}
Tone giọng: {tone}
Thời lượng dự kiến: {targetDuration}
{(!string.IsNullOrWhiteSpace(customPrompt) ? $"Yêu cầu bổ sung: {customPrompt}" : "")}

Hãy tạo kịch bản video ngắn chuẩn cấu trúc:
1. Hook (3s đầu): Đánh vào tâm lý, gây tò mò cực độ.
2. Các phân cảnh (Scenes): Mỗi phân cảnh 1 ý rõ ràng, có lời thoại thuyết minh bằng tiếng Việt (narration) và từ khóa tiếng Anh ngắn gọn trực quan (visualKeyword) để tìm video stock footage.
3. CTA (Call to action): Lời kêu gọi hành động kết video (follow/lưu video/bình luận).

BẮT BUỘC trả về đúng định dạng JSON thuần túy (không kèm markdown code block, không thêm text ngoài JSON):
{{
  ""title"": ""Tiêu đề video hấp dẫn"",
  ""topic"": ""{topic}"",
  ""hook"": ""Câu hook mở đầu 3s"",
  ""scenes"": [
    {{
      ""sceneNo"": 1,
      ""narration"": ""Nội dung thuyết minh phân cảnh 1 bằng tiếng Việt"",
      ""visualDescription"": ""Mô tả hình ảnh trực quan"",
      ""visualKeyword"": ""english stock footage keyword"",
      ""duration"": 6
    }}
  ],
  ""cta"": ""Lời kêu gọi hành động kết video""
}}";
        }

        /// <summary>
        /// Trích xuất nội dung JSON hợp lệ từ phản hồi của AI (loại bỏ markdown block hoặc text thừa)
        /// </summary>
        private static string ExtractJsonContent(string rawText)
        {
            if (string.IsNullOrWhiteSpace(rawText)) return string.Empty;

            var text = rawText.Trim();

            // Tìm vị trí mở ngoặc { đầu tiên và đóng ngoặc } cuối cùng
            var firstBrace = text.IndexOf('{');
            var lastBrace = text.LastIndexOf('}');

            if (firstBrace >= 0 && lastBrace > firstBrace)
            {
                return text.Substring(firstBrace, lastBrace - firstBrace + 1).Trim();
            }

            // Fallback nếu có markdown code block
            if (text.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            {
                text = text.Substring(7);
            }
            else if (text.StartsWith("```", StringComparison.Ordinal))
            {
                text = text.Substring(3);
            }

            if (text.EndsWith("```", StringComparison.Ordinal))
            {
                text = text.Substring(0, text.Length - 3);
            }

            return text.Trim();
        }

        private async Task LogApiUsageAsync(string userId, string provider, int callCount, decimal cost)
        {
            var today = DateTime.UtcNow.Date;
            var log = await _context.ApiUsageLogs
                .FirstOrDefaultAsync(l => l.UserId == userId && l.Provider == provider && l.Date == today);

            if (log == null)
            {
                log = new ApiUsageLog
                {
                    UserId = userId,
                    Provider = provider,
                    CallCount = callCount,
                    CostEstimate = cost,
                    Date = today
                };
                _context.ApiUsageLogs.Add(log);
            }
            else
            {
                log.CallCount += callCount;
                log.CostEstimate += cost;
            }

            await _context.SaveChangesAsync();
        }
    }
}
