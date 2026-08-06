using System;
using System.Collections.Generic;

namespace PostFlow.Models
{
    public class SocialAccount
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; } = string.Empty;
        public PlatformType Platform { get; set; }
        public string AccountName { get; set; } = string.Empty;
        
        /// <summary>
        /// ID cụ thể của kênh YouTube, trang Facebook, hoặc TikTok Profile.
        /// </summary>
        public string ChannelIdOrPageId { get; set; } = string.Empty;
        
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime? TokenExpiresAt { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties cho Entity Framework Core
        public virtual User? User { get; set; }
        public virtual ICollection<UploadJob> UploadJobs { get; set; } = new List<UploadJob>();
    }
}
