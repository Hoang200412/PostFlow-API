using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PostFlow.Configuration;
using PostFlow.Exceptions;

namespace PostFlow.Services.Explainer
{
    /// <summary>Tuỳ chọn cho một lần gọi Gemini: vai trò/quy tắc (system), schema đầu ra và nhiệt độ sinh.</summary>
    /// <param name="SystemInstruction">Vai trò và quy tắc cố định, tách khỏi dữ liệu người dùng.</param>
    /// <param name="ResponseSchema">Schema JSON (dạng OpenAPI subset của Gemini) ép cấu trúc đầu ra.</param>
    /// <param name="Temperature">Ghi đè nhiệt độ mặc định.</param>
    public sealed record GeminiRequestOptions(
        string? SystemInstruction = null,
        object? ResponseSchema = null,
        double? Temperature = null);

    /// <summary>Kết quả một lần gọi Gemini thành công.</summary>
    public sealed record GeminiResult(string Text, string Model);

    public interface IGeminiClient
    {
        /// <summary>Gọi Gemini, thử lần lượt nhiều model, trả về văn bản JSON cùng tên model đã phản hồi.</summary>
        Task<GeminiResult> GenerateAsync(string prompt, GeminiRequestOptions? options = null, CancellationToken cancellationToken = default);

        /// <summary>Tiện ích: chỉ lấy nội dung văn bản (JSON) của phản hồi.</summary>
        async Task<string> GenerateJsonAsync(string prompt, GeminiRequestOptions? options = null, CancellationToken cancellationToken = default) =>
            (await GenerateAsync(prompt, options, cancellationToken)).Text;
    }

    public class GeminiClient : IGeminiClient
    {
        private const string EndpointTemplate = "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent";
        private const string ApiKeyHeader = "x-goog-api-key";
        private const string ApiKeyEnvironmentVariable = "GEMINI_API_KEY";
        internal const double DefaultTemperature = 0.6;
        // Một lần sinh storyboard bình thường mất 30-40s; quá ngưỡng này coi là model đang treo và chuyển model khác.
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(100);

        /// <summary>Chờ trước khi thử lại một lần khi model quá tải (HTTP 503). 429 (hết hạn mức) không thử lại vì chờ vài giây không giúp được.</summary>
        internal static TimeSpan TransientRetryDelay { get; set; } = TimeSpan.FromSeconds(3);

        /// <summary>Danh sách model mặc định (khi AISettings.GeminiModels để trống), thử lần lượt tới khi có kết quả.</summary>
        internal static readonly string[] DefaultModels =
        {
            // Free tier: các model Flash chất lượng cao chỉ có ~20 lượt/ngày; Flash-Lite có 500 lượt/ngày nên làm chỗ dựa chính.
            "gemini-3.8-flash",
            "gemini-3.6-flash",
            "gemini-4-hydrogen",
            "gemini-3.5-flash-lite",
            "gemini-3.1-flash-lite"
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

        private sealed record GeminiResponse(string? Text, string? Error, bool RejectedParameters, bool Transient = false)
        {
            public bool HasText => !string.IsNullOrWhiteSpace(Text);
        }

        public async Task<GeminiResult> GenerateAsync(
            string prompt, GeminiRequestOptions? options = null, CancellationToken cancellationToken = default)
        {
            var apiKey = ResolveApiKey();
            var client = _httpClientFactory.CreateClient();
            client.Timeout = RequestTimeout;

            var errors = new List<string>();
            foreach (var model in ResolveModels())
            {
                try
                {
                    var text = await TryGenerateWithModelAsync(client, apiKey, model, prompt, options, errors, cancellationToken);
                    if (!string.IsNullOrWhiteSpace(text)) return new GeminiResult(text, model);
                }
                // Timeout của HttpClient cũng là OperationCanceledException: chỉ dừng hẳn khi chính người gọi huỷ.
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning(ex, "Model Gemini {Model} thất bại", model);
                    errors.Add($"Gemini {model}: {ex.Message}");
                }
            }

            var details = errors.Count > 0 ? string.Join("; ", errors) : "Không nhận được phản hồi từ các mô hình Gemini AI.";
            throw new BadRequestException($"Gemini AI không phản hồi kết quả hợp lệ. Chi tiết: {details}");
        }

        private IReadOnlyList<string> ResolveModels()
        {
            var configured = _settings.GeminiModels?
                .Where(model => !string.IsNullOrWhiteSpace(model))
                .Select(model => model.Trim())
                .ToList();

            return configured is { Count: > 0 } ? configured : DefaultModels;
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
            HttpClient client, string apiKey, string model, string prompt,
            GeminiRequestOptions? options, List<string> errors, CancellationToken cancellationToken)
        {
            GeminiResponse? response = null;

            // Thử lần lượt từ cấu hình đầy đủ tới cấu hình đơn giản nhất. Chỉ hạ cấp khi API từ chối tham số,
            // để model không hỗ trợ thinking_config hoặc responseSchema vẫn trả được kết quả.
            foreach (var attempt in BuildAttempts(options))
            {
                var payload = BuildPayload(prompt, options, attempt.DisableThinking, attempt.IncludeSchema);
                response = await SendAsync(client, apiKey, model, payload, cancellationToken);

                // Model quá tải (503): chờ rồi thử lại đúng một lần với cùng cấu hình.
                if (response.Transient)
                {
                    await Task.Delay(TransientRetryDelay, cancellationToken);
                    response = await SendAsync(client, apiKey, model, payload, cancellationToken);
                }

                if (response.HasText) return response.Text;
                if (!response.RejectedParameters) break;
            }

            if (response != null && !string.IsNullOrWhiteSpace(response.Error))
            {
                errors.Add($"Gemini {model}: {response.Error}");
            }
            return null;
        }

        private static IEnumerable<(bool DisableThinking, bool IncludeSchema)> BuildAttempts(GeminiRequestOptions? options)
        {
            var hasSchema = options?.ResponseSchema != null;
            yield return (true, hasSchema);
            yield return (false, hasSchema);
            if (hasSchema) yield return (false, false);
        }

        internal static object BuildPayload(string prompt, GeminiRequestOptions? options, bool disableThinking, bool includeSchema)
        {
            var generationConfig = new Dictionary<string, object>
            {
                ["response_mime_type"] = "application/json",
                ["temperature"] = options?.Temperature ?? DefaultTemperature
            };

            if (includeSchema && options?.ResponseSchema != null)
            {
                generationConfig["response_schema"] = options.ResponseSchema;
            }

            if (disableThinking)
            {
                generationConfig["thinking_config"] = new { thinking_budget = 0 };
            }

            var payload = new Dictionary<string, object>
            {
                ["contents"] = new[] { new { role = "user", parts = new[] { new { text = prompt } } } },
                ["generationConfig"] = generationConfig
            };

            if (!string.IsNullOrWhiteSpace(options?.SystemInstruction))
            {
                payload["systemInstruction"] = new { parts = new[] { new { text = options.SystemInstruction } } };
            }

            return payload;
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
                var transient = response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable;
                return new GeminiResponse(null, $"HTTP {(int)response.StatusCode}", rejected, transient);
            }

            var text = ExtractText(body);
            return text == null
                ? new GeminiResponse(null, "Không tìm thấy candidates", RejectedParameters: false)
                : new GeminiResponse(text, null, RejectedParameters: false);
        }

        /// <summary>Lấy phần text cuối cùng không phải "thought" của candidate đầu tiên.</summary>
        internal static string? ExtractText(string responseJson)
        {
            using var document = JsonDocument.Parse(responseJson);
            if (!document.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            {
                return null;
            }

            if (!candidates[0].TryGetProperty("content", out var content)
                || !content.TryGetProperty("parts", out var parts)
                || parts.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            string? text = null;
            foreach (var part in parts.EnumerateArray())
            {
                var isThought = part.TryGetProperty("thought", out var thought) && thought.ValueKind == JsonValueKind.True;
                if (!isThought && part.TryGetProperty("text", out var textElement))
                {
                    text = textElement.GetString();
                }
            }

            if (string.IsNullOrWhiteSpace(text) && parts.GetArrayLength() > 0
                && parts[0].TryGetProperty("text", out var firstText))
            {
                text = firstText.GetString();
            }

            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
    }
}
