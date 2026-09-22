using System;

namespace PostFlow.Models
{
    public class Notification
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        // Foreign Key
        public string UserId { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public NotificationType Type { get; set; } = NotificationType.Info;
        public string? ActionUrl { get; set; }
        public bool IsRead { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? Metadata { get; set; } // JSON metadata

        // Navigation Property
        public virtual User? User { get; set; }
    }
}
