using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace PostFlow.Services.Explainer
{
    /// <summary>Khoảng thời lượng mục tiêu (giây) của video.</summary>
    public readonly record struct DurationRange(int MinSeconds, int MaxSeconds)
    {
        public double MidSeconds => (MinSeconds + MaxSeconds) / 2.0;
    }

    /// <summary>
    /// Xử lý tốc độ nói (pacing), tính toán số từ và làm sạch lời thoại (sanitization) cho giọng đọc TTS.
    /// </summary>
    public static partial class NarrationHelper
    {
        /// <summary>Tốc độ đọc TTS tiếng Việt xấp xỉ (từ/giây) ở nhịp mặc định.</summary>
        public const double WordsPerSecond = 2.8;
        public const int DefaultSeconds = 40;

        [GeneratedRegex(@"\d+")]
        private static partial Regex NumberRegex();

        [GeneratedRegex(@"ph[uú]t|minute|\bmin\b|\bm\b", RegexOptions.IgnoreCase)]
        private static partial Regex MinuteUnitRegex();

        [GeneratedRegex(@"[\uD800-\uDBFF][\uDC00-\uDFFF]")]
        private static partial Regex SurrogatePairRegex();

        [GeneratedRegex(@"[⚡📈📉🛡️💰🔥★☆✓✕•➔→←↑↓©®™■□▲▼▶◀◆◇●○]")]
        private static partial Regex SymbolRegex();

        [GeneratedRegex(@"(\d+)\s*%")]
        private static partial Regex NumberPercentRegex();

        [GeneratedRegex(@"[:;]")]
        private static partial Regex ColonSemicolonRegex();

        [GeneratedRegex(@"(\d+)\.(\d+)")]
        private static partial Regex DecimalRegex();

        [GeneratedRegex(@"(\d+)/(\d+)")]
        private static partial Regex FractionRegex();

        [GeneratedRegex(@"\s+")]
        private static partial Regex WhitespaceRegex();

        // ───────────────────────── Pacing & Target ─────────────────────────

        public static DurationRange ParseTarget(string? target)
        {
            if (string.IsNullOrWhiteSpace(target)) return new DurationRange(DefaultSeconds, DefaultSeconds);

            var numbers = NumberRegex().Matches(target)
                .Select(match => int.TryParse(match.Value, out var value) ? value : 0)
                .Where(value => value > 0)
                .ToList();
            if (numbers.Count == 0) return new DurationRange(DefaultSeconds, DefaultSeconds);

            var multiplier = MinuteUnitRegex().IsMatch(target) ? 60 : 1;
            var first = numbers[0] * multiplier;
            var last = numbers.Count > 1 ? numbers[1] * multiplier : first;
            return new DurationRange(Math.Min(first, last), Math.Max(first, last));
        }

        public static (int Min, int Max) WordBudget(DurationRange range, double lowerFactor = 0.7, double upperFactor = 1.25)
        {
            var min = (int)Math.Floor(range.MinSeconds * WordsPerSecond * lowerFactor);
            var max = (int)Math.Ceiling(range.MaxSeconds * WordsPerSecond * upperFactor);
            return (Math.Max(1, min), Math.Max(min + 1, max));
        }

        public static int TargetWords(DurationRange range) =>
            (int)Math.Round(range.MidSeconds * WordsPerSecond);

        // ───────────────────────── Sanitization ─────────────────────────

        public static string Clean(string? text)
        {
            var result = text ?? string.Empty;
            result = SurrogatePairRegex().Replace(result, " ");
            result = SymbolRegex().Replace(result, " ");
            result = NumberPercentRegex().Replace(result, "$1 phần trăm");
            result = result.Replace("%", " phần trăm ");
            result = ColonSemicolonRegex().Replace(result, ", ");
            result = DecimalRegex().Replace(result, "$1,$2");
            result = FractionRegex().Replace(result, "$1 trên $2");
            return WhitespaceRegex().Replace(result, " ").Trim();
        }

        public static string EnsureTerminalPunctuation(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return text.EndsWith('.') || text.EndsWith('!') || text.EndsWith('?') ? text : text + ".";
        }

        public static int CountWords(string? text) =>
            (text ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
    }
}
