using System.Collections.Generic;

namespace PostFlow.Configuration
{
    public class AISettings
    {
        public OpenRouterSettings OpenRouter { get; set; } = new();
        public string GeminiApiKey { get; set; } = string.Empty;

        /// <summary>Danh sách model Gemini thử lần lượt. Để trống thì dùng danh sách mặc định trong GeminiClient.</summary>
        public List<string> GeminiModels { get; set; } = new();
    }

    public class OpenRouterSettings
    {
        public string ApiKey { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = "https://openrouter.ai/api/v1/chat/completions";
        public List<string> Models { get; set; } = new();
    }
}
