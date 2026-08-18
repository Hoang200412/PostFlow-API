using System;
using System.Collections.Generic;

namespace PostFlow.Models
{
    public class ContentDNA
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; } = string.Empty;
        public string BrandName { get; set; } = string.Empty;
        public string VoiceProfile { get; set; } = string.Empty;
        public string AvatarAssetPath { get; set; } = string.Empty;
        
        /// <summary>
        /// Cấu trúc kịch bản mặc định dạng JSON (Hook/Context/Mechanism/Twist)
        /// </summary>
        public string ScriptStructure { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        public virtual User? User { get; set; }
        public virtual ICollection<VideoAsset> VideoAssets { get; set; } = new List<VideoAsset>();
    }
}
