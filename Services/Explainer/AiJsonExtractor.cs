namespace PostFlow.Services.Explainer
{
    public static class AiJsonExtractor
    {
        /// <summary>Lấy phần JSON object từ phản hồi AI, bỏ qua rào markdown (```json ... ```) và văn bản thừa.</summary>
        public static string Extract(string text)
        {
            var trimmed = text.Trim();

            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                var start = trimmed.IndexOf('\n') + 1;
                var end = trimmed.LastIndexOf("```", StringComparison.Ordinal);
                if (end > start)
                {
                    trimmed = trimmed[start..end].Trim();
                }
            }

            var first = trimmed.IndexOf('{');
            var last = trimmed.LastIndexOf('}');
            return first >= 0 && last > first ? trimmed[first..(last + 1)] : trimmed;
        }
    }
}
