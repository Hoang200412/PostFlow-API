using System;

namespace PostFlow.Models
{
    public class VideoCreationSession
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; } = string.Empty;
        public int Step { get; set; } = 1; // 1 | 2 | 3
        public string IdeaPrompt { get; set; } = string.Empty;
        public string SelectedVoice { get; set; } = "vi-VN-NamMinhNeural";
        public string SelectedStructure { get; set; } = "str_short";
        public string SelectedTone { get; set; } = "Năng động, cuốn hút, bắt trend";
        public string SelectedDuration { get; set; } = "30–45 giây";

        /// <summary>
        /// JSON lưu trữ kịch bản AIScript (title, hook, scenes, cta...) ở Step 2
        /// </summary>
        public string? ScriptJson { get; set; }

        /// <summary>
        /// ID của video tài sản được tạo ra khi bắt đầu Render ở Step 3
        /// </summary>
        public string? CreatedVideoId { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        public virtual User? User { get; set; }
        public virtual VideoAsset? CreatedVideo { get; set; }
    }
}
