using System;
using System.Collections.Generic;

namespace PostFlow.Models
{
    /// <summary>
    /// Một video trên nền tảng được thu thập làm dữ liệu benchmark.
    /// Một video có thể thuộc nhiều nhóm mẫu cùng lúc (chart phổ biến, mẫu viral, mẫu đối chứng).
    /// </summary>
    public class TrendVideo
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Platform { get; set; } = TrendPlatforms.YouTube;
        public string ExternalId { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string EmbedUrl { get; set; } = string.Empty;
        public string ThumbnailUrl { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new();

        public string? TrendChannelId { get; set; }
        public string ChannelExternalId { get; set; } = string.Empty;
        public string ChannelTitle { get; set; } = string.Empty;

        /// <summary>Null khi nền tảng không cho biết thời điểm đăng (ví dụ nhập tay qua oEmbed).</summary>
        public DateTime? PublishedAt { get; set; }
        public int? DurationSeconds { get; set; }
        public bool IsShort { get; set; }

        /// <summary>Danh mục nội bộ: tech, business, lifestyle, entertainment, education.</summary>
        public string CategorySlug { get; set; } = string.Empty;
        public string? PlatformCategoryId { get; set; }
        public string? Language { get; set; }

        /// <summary>Xuất hiện trong chart mostPopular của vùng.</summary>
        public bool InPopularChart { get; set; }

        /// <summary>Xuất hiện trong kết quả search Shorts sắp theo lượt xem (nhóm hiệu suất cao).</summary>
        public bool InViralSample { get; set; }

        /// <summary>Xuất hiện trong mẫu cùng độ tuổi sắp theo ngày đăng (nhóm đối chứng).</summary>
        public bool InControlSample { get; set; }

        public string DataSource { get; set; } = TrendDataSources.YouTubeApi;
        public string? SubmittedByUserId { get; set; }

        /// <summary>Còn được job snapshot theo dõi hay không.</summary>
        public bool IsTracking { get; set; } = true;

        public DateTime FirstSeenAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastSnapshotAt { get; set; }

        // Số liệu của snapshot mới nhất, lưu đúp để đọc nhanh. Nguồn chuẩn vẫn là bảng TrendVideoSnapshots.
        public long? LatestViews { get; set; }
        public long? LatestLikes { get; set; }
        public long? LatestComments { get; set; }

        // Navigation Properties
        public virtual TrendChannel? Channel { get; set; }
        public virtual ICollection<TrendVideoSnapshot> Snapshots { get; set; } = new List<TrendVideoSnapshot>();
    }
}
