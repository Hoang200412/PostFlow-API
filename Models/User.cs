using System;
using System.Collections.Generic;

namespace PostFlow.Models
{
    public class User
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PasswordHash { get; set; }
        public string Role { get; set; } = "User"; // "Admin", "User"
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string? RefreshToken { get; set; }
        public DateTime? RefreshTokenExpiryTime { get; set; }

        // Navigation Properties cho Entity Framework Core
        public virtual ICollection<SocialAccount> SocialAccounts { get; set; } = new List<SocialAccount>();
        public virtual ICollection<VideoAsset> VideoAssets { get; set; } = new List<VideoAsset>();
        public virtual ICollection<UploadJob> UploadJobs { get; set; } = new List<UploadJob>();
    }
}
