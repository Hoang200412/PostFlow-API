using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PostFlow.Configuration;
using PostFlow.Exceptions;

namespace PostFlow.Services.Explainer
{
    public interface IGeminiClient
    {
        /// <summary>Gọi Gemini và trả về nội dung văn bản (JSON) của phản hồi đầu tiên hợp lệ, thử lần lượt nhiều model.</summary>
        Task<string> GenerateJsonAsync(string prompt, CancellationToken cancellationToken = default);
    }

    public class GeminiClient : IGeminiClient
    {
        private const string EndpointTemplate = "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent";
        private const string ApiKeyHeader = "x-goog-api-key";
        private const string ApiKeyEnvironmentVariable = "GEMINI_API_KEY";
        private const double Temperature = 0.6;
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromMinutes(3);

        // Thử lần lượt theo thứ tự cho tới khi có model trả về kết quả.
        private static readonly string[] Models =
        {
            "gemini-2.0-flash",
            "gemini-1.5-flash",
            "gemini-flash-latest",
            "gemini-2.5-flash",
            "gemini-1.5-pro",
            "gemini-3.5-flash",
            "gemini-3.8-flash"
        };

        private static readonly string[] UnsupportedParameterMarkers = { "thinking", "Unknown field", "INVALID_ARGUMENT" };

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly AISettings _settings;
        private readonly ILogger<GeminiClient> _logger;

        public GeminiClient(IHttpClientFactory httpClientFactory, IOptions<AISettings> settings, ILogger<GeminiClient> logger)
        {
            _httpClientFactory = httpClientFactory;
            _settings = settings.Value;
            _logger = logger;
        }

        private sealed record GeminiResponse(string? Text, string? Error, bool RejectedParameters)
        {
            public bool HasText => !string.IsNullOrWhiteSpace(Text);
        }

        public async Task<string> GenerateJsonAsync(string prompt, CancellationToken cancellationToken = default)
        {
            var apiKey = ResolveApiKey();
            var client = _httpClientFactory.CreateClient();
            client.Timeout = RequestTimeout;

            var errors = new List<string>();
            foreach (var model in Models)
            {
                try
                {
                    var text = await TryGenerateWithModelAsync(client, apiKey, model, prompt, errors, cancellationToken);
                    if (!string.IsNullOrWhiteSpace(text)) return text;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Model Gemini {Model} thất bại", model);
                    errors.Add($"Gemini {model}: {ex.Message}");
                }
            }

            var details = errors.Count > 0 ? string.Join("; ", errors) : "Không nhận được phản hồi từ các mô hình Gemini AI.";
            throw new BadRequestException($"Gemini AI không phản hồi kết quả hợp lệ. Chi tiết: {details}");
        }

        private string ResolveApiKey()
        {
            var apiKey = !string.IsNullOrWhiteSpace(_settings.GeminiApiKey)
                ? _settings.GeminiApiKey
                : Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariable);

            return string.IsNullOrWhiteSpace(apiKey)
                ? throw new BadRequestException($"Chưa cấu hình GeminiApiKey trong AISettings hoặc biến môi trường {ApiKeyEnvironmentVariable}.")
                : apiKey;
        }

        private async Task<string?> TryGenerateWithModelAsync(
            HttpClient client, string apiKey, string model, string prompt, List<string> errors, CancellationToken cancellationToken)
        {
            // Lần 1: tắt reasoning (thinking_budget = 0) để nhanh hơn nếu model hỗ trợ.
            var response = await SendAsync(client, apiKey, model, BuildPayload(prompt, disableThinking: true), cancellationToken);

            // Một số model không hỗ trợ thinking_config: gọi lại với cấu hình chuẩn.
            if (!response.HasText && response.RejectedParameters)
            {
                response = await SendAsync(client, apiKey, model, BuildPayload(prompt, disableThinking: false), cancellationToken);
            }

            if (response.HasText) return response.Text;

            if (!string.IsNullOrWhiteSpace(response.Error))
            {
                errors.Add($"Gemini {model}: {response.Error}");
            }
            return null;
        }

        private static object BuildPayload(string prompt, bool disableThinking)
        {
            var generationConfig = new Dictionary<string, object>
            {
                ["response_mime_type"] = "application/json",
                ["temperature"] = Temperature
            };

            if (disableThinking)
            {
                generationConfig["thinking_config"] = new { thinking_budget = 0 };
            }

            return new
            {
                contents = new[] { new { parts = new[] { new { text = prompt } } } },
                generationConfig
            };
        }

        private async Task<GeminiResponse> SendAsync(
            HttpClient client, string apiKey, string model, object payload, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, string.Format(EndpointTemplate, model));
            request.Headers.Add(ApiKeyHeader, apiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var response = await client.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Gemini {Model} trả về HTTP {Code}: {Body}", model, (int)response.StatusCode, body);
                var rejected = response.StatusCode == System.Net.HttpStatusCode.BadRequest
                    && UnsupportedParameterMarkers.Any(marker => body.Contains(marker, StringComparison.OrdinalIgnoreCase));
                return new GeminiResponse(null, $"HTTP {(int)response.StatusCode}", rejected);
            }

            var text = ExtractText(body);
            return text == null
                ? new GeminiResponse(null, "Không tìm thấy candidates", RejectedParameters: false)
                : new GeminiResponse(text, null, RejectedParameters: false);
        }

        /// <summary>Lấy phần text cuối cùng không phải "thought" của candidate đầu tiên.</summary>
        private static string? ExtractText(string responseJson)
        {
            using var document = JsonDocument.Parse(responseJson);
            if (!document.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            {
                return null;
            }

            var parts = candidates[0].GetProperty("content").GetProperty("parts");
            string? text = null;
            foreach (var part in parts.EnumerateArray())
            {
                var isThought = part.TryGetProperty("thought", out var thought) && thought.GetBoolean();
                if (!isThought && part.TryGetProperty("text", out var textElement))
                {
                    text = textElement.GetString();
                }
            }

            if (string.IsNullOrWhiteSpace(text) && parts.GetArrayLength() > 0)
            {
                text = parts[0].GetProperty("text").GetString();
            }

            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
    }
}
