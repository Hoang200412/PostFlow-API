using System.Collections.Generic;

namespace PostFlow.Configuration
{
    public class AISettings
    {
        public OpenRouterSettings OpenRouter { get; set; } = new();
        public string GeminiApiKey { get; set; } = string.Empty;
    }

    public class OpenRouterSettings
    {
        public string ApiKey { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = "https://openrouter.ai/api/v1/chat/completions";
        public List<string> Models { get; set; } = new();
    }
}
