using System.ComponentModel.DataAnnotations;

namespace PostFlow.Models.DTOs.Requests
{
    public class UpsertCreationSessionRequest
    {
        [Range(1, 3)]
        public int Step { get; set; } = 1;

        public string IdeaPrompt { get; set; } = string.Empty;

        public string SelectedVoice { get; set; } = "vi-VN-NamMinhNeural";

        public string SelectedStructure { get; set; } = "str_short";

        public string SelectedTone { get; set; } = "Năng động, cuốn hút, bắt trend";

        public string SelectedDuration { get; set; } = "30–45 giây";

        public string? ScriptJson { get; set; }

        public string? CreatedVideoId { get; set; }
    }
}
