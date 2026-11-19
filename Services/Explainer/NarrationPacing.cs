using System.Text.RegularExpressions;

namespace PostFlow.Services.Explainer
{
    /// <summary>Khoảng thời lượng mục tiêu (giây) của video.</summary>
    public readonly record struct DurationRange(int MinSeconds, int MaxSeconds)
    {
        public double MidSeconds => (MinSeconds + MaxSeconds) / 2.0;
    }

    /// <summary>Quy đổi thời lượng mục tiêu ("30-45 giây", "1 phút") sang số từ lời thoại tương ứng.</summary>
    public static partial class NarrationPacing
    {
        /// <summary>Tốc độ đọc TTS tiếng Việt xấp xỉ (từ/giây) ở nhịp mặc định.</summary>
        public const double WordsPerSecond = 2.8;

        public const int DefaultSeconds = 40;

        [GeneratedRegex(@"\d+")]
        private static partial Regex NumberRegex();

        [GeneratedRegex(@"ph[uú]t|minute|\bmin\b|\bm\b", RegexOptions.IgnoreCase)]
        private static partial Regex MinuteUnitRegex();

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

        /// <summary>Số từ lời thoại tối thiểu/tối đa nên có để video khớp thời lượng (cho phép sai lệch hợp lý).</summary>
        public static (int Min, int Max) WordBudget(DurationRange range, double lowerFactor = 0.7, double upperFactor = 1.25)
        {
            var min = (int)Math.Floor(range.MinSeconds * WordsPerSecond * lowerFactor);
            var max = (int)Math.Ceiling(range.MaxSeconds * WordsPerSecond * upperFactor);
            return (Math.Max(1, min), Math.Max(min + 1, max));
        }

        /// <summary>Số từ mục tiêu lý tưởng (theo thời lượng giữa khoảng).</summary>
        public static int TargetWords(DurationRange range) =>
            (int)Math.Round(range.MidSeconds * WordsPerSecond);
    }
}
