using System.Collections.Generic;

namespace PostFlow.Models.DTOs.Responses
{
    public class SceneDto
    {
        public int SceneNo { get; set; }
        public string Narration { get; set; } = string.Empty;
        public string VisualDescription { get; set; } = string.Empty;
        public string VisualKeyword { get; set; } = string.Empty;
        public int Duration { get; set; } = 5;
    }

    public class ScriptDto
    {
        public string Title { get; set; } = string.Empty;
        public string Topic { get; set; } = string.Empty;
        public string Hook { get; set; } = string.Empty;
        public List<SceneDto> Scenes { get; set; } = new();
        public string Cta { get; set; } = string.Empty;
        public int EstimatedDuration { get; set; } = 30;
        public string? ModelUsed { get; set; }
        public string? ProviderUsed { get; set; }
    }
}
