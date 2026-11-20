using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PostFlow.Data;
using PostFlow.Exceptions;
using PostFlow.Models;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Models.DTOs.Responses;
using PostFlow.Services.AI;
using PostFlow.Services.Interfaces;
using PostFlow.Services.Script;

namespace PostFlow.Services
{
    public class ScriptGenerationService : IScriptGenerationService
    {
        private const decimal CostPerCall = 0.005m;
        private const double RetryTemperature = 0.4;
        private const string ProviderName = "Google Gemini Direct";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly AppDbContext _context;
        private readonly IGeminiClient _gemini;
        private readonly ILogger<ScriptGenerationService> _logger;

        public ScriptGenerationService(
            AppDbContext context,
            IGeminiClient gemini,
            ILogger<ScriptGenerationService> logger)
        {
            _context = context;
            _gemini = gemini;
            _logger = logger;
        }

        /// <summary>
        /// Sinh kịch bản video ngắn bằng Gemini. Prompt tách vai trò (system) khỏi dữ liệu, kèm schema đầu ra;
        /// kết quả được kiểm tra chất lượng và sinh lại một lần nếu chưa đạt.
        /// </summary>
        public async Task<ScriptDto> GenerateScriptAsync(string userId, GenerateScriptRequest request)
        {
            ContentDNA? dna = null;
            if (!string.IsNullOrEmpty(request.ContentDnaId))
            {
                dna = await _context.ContentDNAs
                    .FirstOrDefaultAsync(d => d.Id == request.ContentDnaId && d.UserId == userId);
            }

            var tone = FirstNonEmpty(request.Tone, dna?.VoiceProfile) ?? ScriptPromptBuilder.DefaultTone;
            var targetDuration = FirstNonEmpty(request.TargetDuration) ?? ScriptPromptBuilder.DefaultDuration;

            var calls = 0;
            try
            {
                var script = await GenerateValidatedAsync(request, dna, tone, targetDuration, () => calls++);
                if (script != null)
                {
                    await LogApiUsageAsync(userId, "Gemini", calls, CostPerCall * calls);
                    return script;
                }
            }
            catch (BadRequestException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Gọi Google Gemini API thất bại: {Msg}", ex.Message);
                throw new BadRequestException($"Lỗi khi kết nối với Gemini AI: {ex.Message}");
            }

            _logger.LogError("Không thể sinh kịch bản từ Gemini AI (kết quả trả về rỗng hoặc không đúng định dạng).");
            throw new BadRequestException("Dịch vụ Gemini AI không phản hồi kịch bản hợp lệ. Vui lòng thử lại với chủ đề khác.");
        }

        private async Task<ScriptDto?> GenerateValidatedAsync(
            GenerateScriptRequest request, ContentDNA? dna, string tone, string targetDuration, Action onCall)
        {
            var first = await RequestOnceAsync(ScriptPromptBuilder.Build(request, dna, tone, targetDuration), null, targetDuration, onCall);
            if (first.Script != null && first.Issues.Count == 0) return Finalize(first.Script, request, first.Model);

            var retryIssues = first.Issues.Count > 0
                ? first.Issues
                : new[] { "Đầu ra không phải JSON hợp lệ theo schema." };
            _logger.LogWarning("Kịch bản chưa đạt ({Count} lỗi), sinh lại: {Issues}", retryIssues.Count, string.Join(" | ", retryIssues));

            Attempt second;
            try
            {
                second = await RequestOnceAsync(
                    ScriptPromptBuilder.Build(request, dna, tone, targetDuration, retryIssues), RetryTemperature, targetDuration, onCall);
            }
            catch (Exception ex) when (first.Script != null)
            {
                _logger.LogWarning(ex, "Lần sinh lại kịch bản thất bại, dùng bản đầu tiên.");
                return Finalize(first.Script, request, first.Model);
            }

            var best = PickBest(first, second);
            if (best == null) return null;

            if (best.Issues.Count > 0)
            {
                _logger.LogWarning("Dùng kịch bản còn {Count} cảnh báo: {Issues}", best.Issues.Count, string.Join(" | ", best.Issues));
            }
            return Finalize(best.Script!, request, best.Model);
        }

        private async Task<Attempt> RequestOnceAsync(
            ScriptPrompt prompt, double? temperature, string targetDuration, Action onCall)
        {
            onCall();
            var result = await _gemini.GenerateAsync(
                prompt.User,
                new GeminiRequestOptions(prompt.System, GeminiSchemas.Script(), temperature));

            ScriptDto? script;
            try
            {
                script = JsonSerializer.Deserialize<ScriptDto>(AiJsonExtractor.Extract(result.Text), JsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Không đọc được JSON kịch bản từ AI.");
                return new Attempt(null, Array.Empty<string>(), result.Model);
            }

            if (script == null) return new Attempt(null, Array.Empty<string>(), result.Model);

            Normalize(script);
            return new Attempt(script, ScriptOutputValidator.ValidateScript(script, targetDuration), result.Model);
        }

        private static Attempt? PickBest(Attempt first, Attempt second)
        {
            if (second.Script == null) return first.Script == null ? null : first;
            if (first.Script == null) return second;
            return second.Issues.Count <= first.Issues.Count ? second : first;
        }

        private static ScriptDto? Finalize(ScriptDto script, GenerateScriptRequest request, string model)
        {
            if (script.Scenes.Count == 0) return null;

            if (string.IsNullOrWhiteSpace(script.Topic)) script.Topic = request.Topic;
            script.EstimatedDuration = script.Scenes.Sum(scene => scene.Duration);
            script.ModelUsed = $"google/{model}";
            script.ProviderUsed = ProviderName;
            return script;
        }

        /// <summary>Loại bỏ cảnh trống, đánh số lại liên tục, đảm bảo duration dương.</summary>
        internal static void Normalize(ScriptDto script)
        {
            script.Scenes = (script.Scenes ?? new List<SceneDto>())
                .Where(scene => !string.IsNullOrWhiteSpace(scene.Narration))
                .ToList();

            for (var i = 0; i < script.Scenes.Count; i++)
            {
                script.Scenes[i].SceneNo = i + 1;
                if (script.Scenes[i].Duration <= 0) script.Scenes[i].Duration = 1;
            }
        }

        private static string? FirstNonEmpty(params string?[] values) =>
            values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

        private sealed record Attempt(ScriptDto? Script, IReadOnlyList<string> Issues, string Model);

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
