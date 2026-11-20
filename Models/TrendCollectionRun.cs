using System;

namespace PostFlow.Models
{
    /// <summary>Nhật ký mỗi lượt thu thập: dùng để theo dõi quota, lỗi và làm số liệu cho báo cáo.</summary>
    public class TrendCollectionRun
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>discover, snapshot hoặc import.</summary>
        public string Kind { get; set; } = string.Empty;

        /// <summary>popular, viral, control, channels, snapshot hoặc manual.</summary>
        public string Source { get; set; } = string.Empty;
        public string Platform { get; set; } = TrendPlatforms.YouTube;
        public string? CategorySlug { get; set; }

        public string Status { get; set; } = TrendRunStatuses.Succeeded;
        public int VideosFound { get; set; }
        public int VideosNew { get; set; }
        public int SnapshotsWritten { get; set; }
        public int QuotaUnitsUsed { get; set; }
        public string? ErrorMessage { get; set; }

        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? FinishedAt { get; set; }
    }
}
