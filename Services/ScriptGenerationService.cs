using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PostFlow.Data;
using PostFlow.Exceptions;
using PostFlow.Models;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Models.DTOs.Responses;
using PostFlow.Services.Interfaces;

namespace PostFlow.Services;

public class ScriptGenerationService : IScriptGenerationService
{
    private readonly AppDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ScriptGenerationService> _logger;

    public ScriptGenerationService(
        AppDbContext context,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<ScriptGenerationService> logger)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Sinh kịch bản video AI theo chủ đề và Content DNA (Structured JSON Output)
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

        var structureId = !string.IsNullOrWhiteSpace(request.StructureId)
            ? request.StructureId
            : (!string.IsNullOrWhiteSpace(dna?.ScriptStructure) ? dna.ScriptStructure : "hook_3scenes_cta");

        var openRouterKey = _configuration["AI:OpenRouter:ApiKey"]
            ?? Environment.GetEnvironmentVariable("OPENROUTER_API_KEY")
            ?? _configuration["OpenRouter:ApiKey"];

        if (!string.IsNullOrEmpty(openRouterKey))
        {
            try
            {
                var openRouterScript = await CallOpenRouterForScriptAsync(openRouterKey, request.Topic, tone, structureId, request.CustomPrompt);
                if (openRouterScript != null && openRouterScript.Scenes.Count > 0)
                {
                    await LogApiUsageAsync(userId, "OpenRouter", 1, 0.003m);
                    return openRouterScript;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Gọi OpenRouter API thất bại, chuyển sang bộ sinh tiếp theo: {Msg}", ex.Message);
            }
        }

        var geminiKey = _configuration["AI:GeminiApiKey"]
            ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY")
            ?? _configuration["Gemini:ApiKey"];

        if (!string.IsNullOrEmpty(geminiKey))
        {
            try
            {
                var geminiScript = await CallGeminiForScriptAsync(geminiKey, request.Topic, tone, request.CustomPrompt);
                if (geminiScript != null && geminiScript.Scenes.Count > 0)
                {
                    await LogApiUsageAsync(userId, "Gemini", 1, 0.005m);
                    return geminiScript;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Gọi Gemini Direct API thất bại, chuyển sang bộ sinh kịch bản chuẩn hóa: {Msg}", ex.Message);
            }
        }

        _logger.LogError("Không thể sinh kịch bản từ AI do thiếu API Key hoặc tất cả các AI Provider (OpenRouter, Gemini) đều gặp lỗi/hết quota.");
        throw new BadRequestException("Không thể kết nối hoặc sinh kịch bản từ dịch vụ AI. Vui lòng kiểm tra lại cấu hình API Key (OpenRouter / Gemini) hoặc hạn mức Quota.");
    }

    private async Task<ScriptDto?> CallOpenRouterForScriptAsync(string apiKey, string topic, string tone, string structureId, string? customPrompt)
    {
        var client = _httpClientFactory.CreateClient();
        var url = _configuration["AI:OpenRouter:BaseUrl"] ?? "https://openrouter.ai/api/v1/chat/completions";

        var configuredModels = _configuration.GetSection("AI:OpenRouter:Models").Get<string[]>() ?? new[]
        {
            "google/gemini-1.5-flash",
            "google/gemini-2.0-flash-001",
            "deepseek/deepseek-chat",
            "openai/gpt-4o-mini",
            "meta-llama/llama-3.3-70b-instruct"
        };

        var promptText = $@"Bạn là chuyên gia sáng tạo kịch bản video ngắn (TikTok, YouTube Shorts, Reels) hàng đầu.
            Chủ đề: {topic}
            Tone giọng: {tone}
            {(!string.IsNullOrEmpty(customPrompt) ? $"Yêu cầu bổ sung: {customPrompt}" : "")}

            Hãy tạo kịch bản video ngắn 30-45 giây chuẩn cấu trúc:
            1. Hook (3s đầu): Đánh vào tâm lý, gây tò mò cực độ.
            2. Các phân cảnh (Scenes): Mỗi phân cảnh 1 ý rõ ràng, có lời thoại thuyết minh bằng tiếng Việt và từ khóa tiếng Anh visualKeyword (ngắn gọn, trực quan) để tìm video stock footage.
            3. CTA (Call to action): Kêu gọi follow/lưu video.

            BẮT BUỘC trả về đúng định dạng JSON thuần túy (không markdown codeblock, không thêm text ngoài JSON):
            {{
            ""title"": ""Tiêu đề video hấp dẫn"",
            ""topic"": ""{topic}"",
            ""hook"": ""Câu hook mở đầu 3s"",
            ""scenes"": [
                {{
                ""sceneNo"": 1,
                ""narration"": ""Nội dung thuyết minh phân cảnh 1 bằng tiếng Việt"",
                ""visualDescription"": ""Mô tả hình ảnh phân cảnh 1"",
                ""visualKeyword"": ""english stock footage keyword"",
                ""duration"": 6
                }}
            ],
            ""cta"": ""Lời kêu gọi hành động kết video""
        }}";

        var requestBody = new
        {
            models = configuredModels,
            messages = new object[]
            {
                new { role = "system", content = "Bạn là AI chuyên gia sáng tạo kịch bản video ngắn triệu view. Luôn phản hồi JSON hợp lệ theo đúng schema được yêu cầu." },
                new { role = "user", content = promptText }
            },
            response_format = new { type = "json_object" },
            temperature = 0.7
        };

        var requestMessage = new HttpRequestMessage(HttpMethod.Post, url);
        requestMessage.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        requestMessage.Headers.Add("HTTP-Referer", "https://postflow.io");
        requestMessage.Headers.Add("X-Title", "PostFlow AI");
        requestMessage.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        var response = await client.SendAsync(requestMessage);

        if (!response.IsSuccessStatusCode)
        {
            var errContent = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("OpenRouter API error {StatusCode}: {Error}", response.StatusCode, errContent);
            return null;
        }

        var responseJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseJson);

        var root = doc.RootElement;
        var modelUsed = root.TryGetProperty("model", out var modelProp) ? modelProp.GetString() : "openrouter/auto";

        var choices = root.GetProperty("choices");
        if (choices.GetArrayLength() == 0) return null;

        var contentText = choices[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        if (string.IsNullOrEmpty(contentText)) return null;

        var cleanJson = ExtractJsonContent(contentText);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var script = JsonSerializer.Deserialize<ScriptDto>(cleanJson, options);

        if (script != null && script.Scenes.Count > 0)
        {
            script.EstimatedDuration = script.Scenes.Sum(s => s.Duration);
            script.ModelUsed = modelUsed;
            script.ProviderUsed = "OpenRouter";
        }

        return script;
    }

    private async Task<ScriptDto?> CallGeminiForScriptAsync(string apiKey, string topic, string tone, string? customPrompt)
    {
        var client = _httpClientFactory.CreateClient();
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={apiKey}";

        var promptText = $@"Bạn là chuyên gia sáng tạo kịch bản video ngắn (TikTok, YouTube Shorts, Reels) hàng đầu.
            Chủ đề: {topic}
            Tone giọng: {tone}
            {(!string.IsNullOrEmpty(customPrompt) ? $"Yêu cầu bổ sung: {customPrompt}" : "")}

            Hãy tạo kịch bản video ngắn 30-45 giây chuẩn cấu trúc:
            1. Hook (3s đầu): Đánh vào tâm lý, gây tò mò cực độ.
            2. Các phân cảnh (Scenes): Mỗi phân cảnh 1 ý rõ ràng, có lời thoại và từ khóa tiếng Anh để tìm video stock.
            3. CTA (Call to action): Kêu gọi follow/lưu video.

            BẮT BUỘC trả về đúng định dạng JSON thuần túy (không markdown block):
            {{
            ""title"": ""Tiêu đề video hấp dẫn"",
            ""topic"": ""{topic}"",
            ""hook"": ""Câu hook mở đầu 3s"",
            ""scenes"": [
                {{
                ""sceneNo"": 1,
                ""narration"": ""Nội dung thuyết minh phân cảnh 1"",
                ""visualDescription"": ""Mô tả hình ảnh"",
                ""visualKeyword"": ""english visual keyword for stock video search"",
                ""duration"": 5
                }}
            ],
            ""cta"": ""Lời kêu gọi hành động kết video""
        }}";

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

        var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
        var response = await client.PostAsync(url, jsonContent);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Gemini API returned status code {StatusCode}", response.StatusCode);
            return null;
        }

        var responseJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseJson);

        var candidateText = doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        if (string.IsNullOrEmpty(candidateText)) return null;

        var cleanJson = ExtractJsonContent(candidateText);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var script = JsonSerializer.Deserialize<ScriptDto>(cleanJson, options);

        if (script != null && script.Scenes.Count > 0)
        {
            script.EstimatedDuration = script.Scenes.Sum(s => s.Duration);
            script.ModelUsed = "google/gemini-1.5-flash";
            script.ProviderUsed = "Google Gemini Direct";
        }

        return script;
    }

    private static string ExtractJsonContent(string rawText)
    {
        var text = rawText.Trim();
        if (text.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            text = text.Substring(7);
        }
        else if (text.StartsWith("```"))
        {
            text = text.Substring(3);
        }

        if (text.EndsWith("```"))
        {
            text = text.Substring(0, text.Length - 3);
        }

        return text.Trim();
    }

    private async Task LogApiUsageAsync(string userId, string provider, int callCount, decimal cost)
    {
        var today = DateTime.Today;
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
