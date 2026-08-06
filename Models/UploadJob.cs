using System;

namespace PostFlow.Models
{
    public class UploadJob
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        // Foreign Keys
        public string UserId { get; set; } = string.Empty;
        public string VideoAssetId { get; set; } = string.Empty;
        public string SocialAccountId { get; set; } = string.Empty;

        public PlatformType Platform { get; set; } // Lưu đúp cho mục đích filter nhanh
        public UploadStatus Status { get; set; } = UploadStatus.Pending;
        public int ProgressPercentage { get; set; }
        public string? ErrorMessage { get; set; }
        public string? VideoUrl { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }

        // Navigation Properties cho EF Core
        public virtual User? User { get; set; }
        public virtual VideoAsset? VideoAsset { get; set; }
        public virtual SocialAccount? SocialAccount { get; set; }
    }
}
