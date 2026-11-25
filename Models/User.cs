using System;
using System.Collections.Generic;

namespace PostFlow.Models
{
    public static class UserPlans
    {
        public const string Trial = "Trial";
        public const string Pro = "Pro";
    }

    public class User
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PasswordHash { get; set; }
        public string Role { get; set; } = "User"; // "Admin", "User"
        public string? AvatarUrl { get; set; }
        public bool IsActive { get; set; } = true;

        /// <summary>Gói dịch vụ của tài khoản: "Trial" (mặc định) hoặc "Pro". Quyết định hạn mức gọi AI mỗi ngày.</summary>
        public string Plan { get; set; } = UserPlans.Trial;

        /// <summary>Hạn mức AI/ngày riêng cho user này (ghi đè hạn mức theo gói). null = dùng theo gói, -1 = không giới hạn.</summary>
        public int? DailyAiLimitOverride { get; set; }

        /// <summary>Thời điểm xóa mềm. Khác null nghĩa là tài khoản đã bị xóa và bị ẩn khỏi mọi truy vấn thông thường.</summary>
        public DateTime? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string? RefreshToken { get; set; }
        public DateTime? RefreshTokenExpiryTime { get; set; }

        // Navigation Properties cho Entity Framework Core
        public virtual ICollection<SocialAccount> SocialAccounts { get; set; } = new List<SocialAccount>();
        public virtual ICollection<VideoAsset> VideoAssets { get; set; } = new List<VideoAsset>();
        public virtual ICollection<UploadJob> UploadJobs { get; set; } = new List<UploadJob>();
        public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
        public virtual ICollection<ContentDNA> ContentDNAs { get; set; } = new List<ContentDNA>();
        public virtual ICollection<ApiUsageLog> ApiUsageLogs { get; set; } = new List<ApiUsageLog>();
        public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
        public virtual VideoCreationSession? VideoCreationSession { get; set; }
    }
}
