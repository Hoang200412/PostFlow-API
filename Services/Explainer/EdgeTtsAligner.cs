using System.Globalization;
using System.Text.RegularExpressions;
using PostFlow.Services.Interfaces;

namespace PostFlow.Services.Explainer
{
    public partial class EdgeTtsAligner : IEdgeTtsAligner
    {
        private readonly ITtsService _ttsService;
        private readonly ILogger<EdgeTtsAligner> _logger;

        public EdgeTtsAligner(ITtsService ttsService, ILogger<EdgeTtsAligner> logger)
        {
            _ttsService = ttsService;
            _logger = logger;
        }

        public async Task<NarrationAudio> SynthesizeAsync(
            string narrationText,
            VoiceOptions voice,
            string workDirectory,
            CancellationToken cancellationToken = default)
        {
            var audioPath = Path.Combine(workDirectory, "narration.mp3");

            await _ttsService.SynthesizeToFileAsync(
                narrationText,
                voice.Voice,
                voice.Rate,
                voice.Pitch,
                audioPath,
                cancellationToken);

            var totalSeconds = await ProbeDurationSecondsAsync(audioPath, cancellationToken);
            var words = EstimateWordTimings(narrationText, totalSeconds);

            _logger.LogInformation(
                "Đã sinh audio Explainer qua PostFlow-TTS (duration={Duration:F2}s, words={WordCount}).",
                totalSeconds,
                words.Count);

            return new NarrationAudio(audioPath, totalSeconds, words);
        }

        private async Task<double> ProbeDurationSecondsAsync(string audioPath, CancellationToken cancellationToken)
        {
            var ffprobe = GetFfprobeExecutable();
            var result = await ExternalProcessRunner.RunAsync(
                ffprobe,
                new[]
                {
                    "-v", "error",
                    "-show_entries", "format=duration",
                    "-of", "default=noprint_wrappers=1:nokey=1",
                    audioPath
                },
                cancellationToken: cancellationToken);

            if (!result.Succeeded ||
                !double.TryParse(result.StandardOutput.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) ||
                seconds <= 0)
            {
                _logger.LogWarning("ffprobe không đọc được thời lượng audio ({Stderr}); dùng ước lượng theo số từ.", result.StandardError);
                return 0;
            }

            return seconds;
        }

        /// <summary>
        /// Ước lượng phân bổ timestamp cho từng từ dựa trên tổng thời lượng audio và độ dài ký tự của từ.
        /// </summary>
        internal static List<SpokenWord> EstimateWordTimings(string text, double totalSeconds)
        {
            var tokens = WhitespaceRegex().Split(text.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList();

            if (tokens.Count == 0)
            {
                return new List<SpokenWord>();
            }

            var totalMs = totalSeconds > 0
                ? totalSeconds * 1000.0
                : tokens.Count * 380.0; // Fallback ~380ms/từ nếu ffprobe không khả dụng

            // Trọng số mỗi từ = số ký tự chữ/số (tối thiểu 1) + pause nhẹ ở dấu câu kết thúc
            var weights = tokens.Select(token =>
            {
                var coreLen = Math.Max(1, token.Count(char.IsLetterOrDigit));
                var punctuationPause = token.EndsWith('.') || token.EndsWith('!') || token.EndsWith('?') ? 2.5
                    : token.EndsWith(',') || token.EndsWith(';') || token.EndsWith(':') ? 1.2
                    : 0.0;
                return coreLen + punctuationPause;
            }).ToList();

            var totalWeight = weights.Sum();
            var words = new List<SpokenWord>(tokens.Count);
            var cursorMs = 0.0;

            for (var i = 0; i < tokens.Count; i++)
            {
                var durationMs = (weights[i] / totalWeight) * totalMs;
                var endMs = i == tokens.Count - 1 ? totalMs : cursorMs + durationMs;
                words.Add(new SpokenWord(tokens[i], Math.Round(cursorMs, 1), Math.Round(endMs, 1)));
                cursorMs = endMs;
            }

            return words;
        }

        private static string GetFfprobeExecutable()
        {
            var localFfprobe = Path.Combine(AppContext.BaseDirectory, "tools", "ffprobe.exe");
            if (File.Exists(localFfprobe)) return localFfprobe;

            var toolsFfprobe = Path.Combine(Directory.GetCurrentDirectory(), "tools", "ffprobe.exe");
            if (File.Exists(toolsFfprobe)) return toolsFfprobe;

            return "ffprobe";
        }

        [GeneratedRegex(@"\s+")]
        private static partial Regex WhitespaceRegex();
    }
}
