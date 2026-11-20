using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PostFlow.Configuration;
using PostFlow.Exceptions;
using PostFlow.Services.Interfaces;

namespace PostFlow.Services.AI
{
    public class GeminiClient : IGeminiClient
    {
        private const string EndpointTemplate = "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent";
        private const string ApiKeyHeader = "x-goog-api-key";
        private const string ApiKeyEnvironmentVariable = "GEMINI_API_KEY";
        internal const double DefaultTemperature = 0.6;
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(100);

        internal static TimeSpan TransientRetryDelay { get; set; } = TimeSpan.FromSeconds(3);

        internal static readonly string[] DefaultModels =
        {
            "gemini-3.8-flash",
            "gemini-3.6-flash",
            "gemini-4-hydrogen",
            "gemini-3.5-flash-lite",
            "gemini-3.1-flash-lite"
        };

        private static readonly string[] UnsupportedParameterMarkers = { "thinking", "Unknown field", "INVALID_ARGUMENT", "tools", "googleSearch" };

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

            foreach (var attempt in BuildAttempts(options))
            {
                var payload = BuildPayload(prompt, options, attempt.DisableThinking, attempt.IncludeSchema, attempt.IncludeTools);
                response = await SendAsync(client, apiKey, model, payload, cancellationToken);

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

        private static IEnumerable<(bool DisableThinking, bool IncludeSchema, bool IncludeTools)> BuildAttempts(GeminiRequestOptions? options)
        {
            var hasSchema = options?.ResponseSchema != null;
            var hasTools = options?.EnableGoogleSearch == true;

            // Attempt 1: Gửi cấu hình đầy đủ (kèm thinking_config và tools nếu có)
            yield return (DisableThinking: true, IncludeSchema: hasSchema, IncludeTools: hasTools);

            // Attempt 2: Nếu có tools và bị lỗi parameter, bỏ tools
            if (hasTools)
            {
                yield return (DisableThinking: true, IncludeSchema: hasSchema, IncludeTools: false);
            }

            // Attempt 3: Bỏ thinking_config (để tương thích model không hỗ trợ trường này)
            yield return (DisableThinking: false, IncludeSchema: hasSchema, IncludeTools: false);

            // Attempt 4: Hạ cấp cuối cùng, tắt schema để model trả text tự do
            if (hasSchema)
            {
                yield return (DisableThinking: false, IncludeSchema: false, IncludeTools: false);
            }
        }

        internal static object BuildPayload(
            string prompt,
            GeminiRequestOptions? options,
            bool disableThinking,
            bool includeSchema,
            bool includeTools = false)
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
                var budget = options?.ThinkingBudget ?? 0;
                generationConfig["thinking_config"] = new { thinking_budget = budget };
            }

            var contents = new List<object>();
            if (options?.ConversationHistory is { Count: > 0 })
            {
                foreach (var msg in options.ConversationHistory)
                {
                    contents.Add(new { role = msg.Role, parts = new[] { new { text = msg.Text } } });
                }
            }

            if (!string.IsNullOrWhiteSpace(prompt))
            {
                contents.Add(new { role = "user", parts = new[] { new { text = prompt } } });
            }

            var payload = new Dictionary<string, object>
            {
                ["contents"] = contents,
                ["generationConfig"] = generationConfig
            };

            if (includeTools && options?.EnableGoogleSearch == true)
            {
                payload["tools"] = new[] { new { googleSearch = new { } } };
            }

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
