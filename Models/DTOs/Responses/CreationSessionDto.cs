using System;

namespace PostFlow.Models.DTOs.Responses
{
    public class CreationSessionDto
    {
        public string Id { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public int Step { get; set; } = 1;
        public string IdeaPrompt { get; set; } = string.Empty;
        public string SelectedVoice { get; set; } = "vi-VN-NamMinhNeural";
        public string SelectedStructure { get; set; } = "str_short";
        public string SelectedTone { get; set; } = "Năng động, cuốn hút, bắt trend";
        public string SelectedDuration { get; set; } = "30–45 giây";
        public string? ScriptJson { get; set; }
        public string? CreatedVideoId { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public static class CreationSessionMappingExtensions
    {
        public static CreationSessionDto ToDto(this VideoCreationSession session)
        {
            if (session == null) return null!;

            return new CreationSessionDto
            {
                Id = session.Id,
                UserId = session.UserId,
                Step = session.Step,
                IdeaPrompt = session.IdeaPrompt,
                SelectedVoice = session.SelectedVoice,
                SelectedStructure = session.SelectedStructure,
                SelectedTone = session.SelectedTone,
                SelectedDuration = session.SelectedDuration,
                ScriptJson = session.ScriptJson,
                CreatedVideoId = session.CreatedVideoId,
                UpdatedAt = session.UpdatedAt
            };
        }
    }
}
