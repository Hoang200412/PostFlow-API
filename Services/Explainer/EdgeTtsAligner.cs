using System.Text;
using System.Text.Json;
using PostFlow.Services.Interfaces;

namespace PostFlow.Services.Explainer
{
    public class EdgeTtsAligner : IEdgeTtsAligner
    {
        private const string DefaultVoice = "vi-VN-NamMinhNeural";
        private const string DefaultRate = "+0%";
        private const string DefaultPitch = "+0Hz";

        private readonly ExplainerToolLocator _tools;
        private readonly ILogger<EdgeTtsAligner> _logger;

        public EdgeTtsAligner(ExplainerToolLocator tools, ILogger<EdgeTtsAligner> logger)
        {
            _tools = tools;
            _logger = logger;
        }

        public async Task<NarrationAudio> SynthesizeAsync(
            string narrationText,
            VoiceOptions voice,
            string workDirectory,
            CancellationToken cancellationToken = default)
        {
            var textPath = Path.Combine(workDirectory, "narration.txt");
            var audioPath = Path.Combine(workDirectory, "narration.mp3");
            var timingPath = Path.Combine(workDirectory, "timing.json");

            await File.WriteAllTextAsync(textPath, narrationText, new UTF8Encoding(false), cancellationToken);

            var result = await ExternalProcessRunner.RunAsync(
                _tools.PythonExecutable,
                new[]
                {
                    _tools.EdgeTtsAlignerScriptPath,
                    "--text-file", textPath,
                    "--voice", OrDefault(voice.Voice, DefaultVoice),
                    "--rate", OrDefault(voice.Rate, DefaultRate),
                    "--pitch", OrDefault(voice.Pitch, DefaultPitch),
                    "--output-audio", audioPath,
                    "--output-json", timingPath
                },
                cancellationToken: cancellationToken);

            if (!result.Succeeded || !File.Exists(audioPath) || !File.Exists(timingPath))
            {
                _logger.LogError("edge_tts_aligner.py thất bại (exit {ExitCode}): {Stderr}", result.ExitCode, result.StandardError);
                throw new InvalidOperationException($"Lỗi sinh audio Edge-TTS: {result.StandardError}");
            }

            return await ParseTimingAsync(audioPath, timingPath, cancellationToken);
        }

        private static async Task<NarrationAudio> ParseTimingAsync(string audioPath, string timingPath, CancellationToken cancellationToken)
        {
            var json = await File.ReadAllTextAsync(timingPath, cancellationToken);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            var totalSeconds = root.GetProperty("total_duration_seconds").GetDouble();
            var words = new List<SpokenWord>();

            if (root.TryGetProperty("words", out var wordsElement))
            {
                foreach (var word in wordsElement.EnumerateArray())
                {
                    words.Add(new SpokenWord(
                        word.GetProperty("text").GetString() ?? string.Empty,
                        word.GetProperty("start_ms").GetDouble(),
                        word.GetProperty("end_ms").GetDouble()));
                }
            }

            return new NarrationAudio(audioPath, totalSeconds, words);
        }

        private static string OrDefault(string? value, string fallback) =>
            string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}
