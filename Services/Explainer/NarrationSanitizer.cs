using System.Text.RegularExpressions;

namespace PostFlow.Services.Explainer
{
    /// <summary>Tiền xử lý lời thoại tiếng Việt cho TTS: bỏ emoji/ký hiệu, đọc được số thập phân, phần trăm, phân số.</summary>
    public static partial class NarrationSanitizer
    {
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

        /// <summary>Làm sạch một đoạn lời thoại, không thêm dấu kết câu.</summary>
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

        /// <summary>Đảm bảo văn bản kết thúc bằng dấu câu để TTS không bị cụt câu.</summary>
        public static string EnsureTerminalPunctuation(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return text.EndsWith('.') || text.EndsWith('!') || text.EndsWith('?') ? text : text + ".";
        }

        public static int CountWords(string? text) =>
            (text ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
    }
}
