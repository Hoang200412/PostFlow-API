using System;
using System.Collections.Generic;

namespace PostFlow.Models
{
    /// <summary>Kênh/tác giả của video benchmark. Dùng để chuẩn hóa theo quy mô kênh.</summary>
    public class TrendChannel
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Platform { get; set; } = TrendPlatforms.YouTube;
        public string ExternalId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string AvatarUrl { get; set; } = string.Empty;

        /// <summary>Null khi kênh ẩn số subscriber hoặc nền tảng không cung cấp.</summary>
        public long? SubscriberCount { get; set; }
        public long? VideoCount { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Property
        public virtual ICollection<TrendVideo> Videos { get; set; } = new List<TrendVideo>();
    }
}
