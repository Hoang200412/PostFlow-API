using System;
using System.Collections.Generic;

namespace PostFlow.Models.DTOs.Responses
{
    /// <summary>
    /// Một video xu hướng trả về cho UI. Mọi số liệu đều đến từ snapshot thật đã lưu;
    /// field nào không có dữ liệu thì để null/rỗng, không điền giá trị minh họa.
    /// </summary>
    public class TrendTopicDto
    {
        public string Id { get; set; } = string.Empty;
        public string ExternalId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty; // youtube, tiktok, reels
        public string Category { get; set; } = string.Empty; // tech, business, lifestyle, entertainment, education
        public string VideoUrl { get; set; } = string.Empty;
        public string EmbedUrl { get; set; } = string.Empty;
        public string AuthorName { get; set; } = string.Empty;
        public string AuthorAvatarUrl { get; set; } = string.Empty;
        public string ThumbnailUrl { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> Hashtags { get; set; } = new();

        /// <summary>Không có API nào trả về câu hook 3 giây đầu; để rỗng thay vì để AI đoán.</summary>
        public string SampleHook { get; set; } = string.Empty;

        // Số liệu dạng hiển thị (đã định dạng sẵn: "1.2M"). Rỗng khi không có dữ liệu.
        public string ViewsCount { get; set; } = string.Empty;
        public string LikesCount { get; set; } = string.Empty;
        public string Duration { get; set; } = string.Empty;
        public string GrowthRate { get; set; } = string.Empty;

        // Số liệu thô.
        public long? Views { get; set; }
        public long? Likes { get; set; }
        public long? Comments { get; set; }
        public long? SubscriberCount { get; set; }
        public int? DurationSeconds { get; set; }
        public bool IsShort { get; set; }

        // Chỉ số suy ra.
        public double? AgeHours { get; set; }
        public double? ViewsPerHour { get; set; }
        public double? EngagementRate { get; set; }
        public double? ViewsPerSubscriber { get; set; }

        /// <summary>Tăng trưởng views quy về 24h giữa hai snapshot gần nhất (0.35 = +35%).</summary>
        public double? Growth24h { get; set; }

        /// <summary>
        /// Thứ hạng phần trăm (0-100) theo views/giờ so với các video cùng nền tảng và danh mục trong tập dữ liệu.
        /// Null khi chưa đủ video để so sánh.
        /// </summary>
        public int? ViralityScore { get; set; }

        /// <summary>popular, viral, control, manual.</summary>
        public List<string> SampleTypes { get; set; } = new();
        public string DataSource { get; set; } = string.Empty;
        public int SnapshotCount { get; set; }

        public DateTime? PublishedAt { get; set; }
        public DateTime FirstSeenAt { get; set; }
        public DateTime? LastUpdatedAt { get; set; }

        /// <summary>Giữ tương thích với UI cũ: thời điểm đăng nếu biết, nếu không là lúc hệ thống ghi nhận.</summary>
        public DateTime TrendingSince { get; set; }
    }

    public class TrendSnapshotDto
    {
        public DateTime CapturedAt { get; set; }
        public double? AgeHours { get; set; }
        public int? MilestoneHours { get; set; }
        public long? Views { get; set; }
        public long? Likes { get; set; }
        public long? Comments { get; set; }
        public string DataSource { get; set; } = string.Empty;
    }
}
