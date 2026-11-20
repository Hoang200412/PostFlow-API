using System;
using System.Collections.Generic;

namespace PostFlow.Models.DTOs.Responses
{
    /// <summary>Tình trạng bộ dữ liệu benchmark: dùng để theo dõi thu thập và làm số liệu cho báo cáo.</summary>
    public class TrendCollectionStatsDto
    {
        public bool CollectorConfigured { get; set; }
        public bool CollectorEnabled { get; set; }
        public int TotalVideos { get; set; }
        public int TrackingVideos { get; set; }
        public int TotalSnapshots { get; set; }

        /// <summary>Số video có từ 2 snapshot trở lên (đủ để tính tốc độ tăng).</summary>
        public int VideosWithMultipleSnapshots { get; set; }
        public int TotalChannels { get; set; }
        public Dictionary<string, int> VideosByPlatform { get; set; } = new();
        public Dictionary<string, int> VideosByCategory { get; set; } = new();
        public Dictionary<string, int> VideosBySample { get; set; } = new();
        public Dictionary<string, int> VideosByDataSource { get; set; } = new();
        public Dictionary<string, int> SnapshotsByMilestone { get; set; } = new();
        public int QuotaUsedToday { get; set; }
        public int QuotaDailyBudget { get; set; }
        public bool ProviderQuotaExceeded { get; set; }
        public DateTime? LastCollectedAt { get; set; }
        public List<TrendCollectionRunDto> RecentRuns { get; set; } = new();
    }

    public class TrendCollectionRunDto
    {
        public string Kind { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string? Category { get; set; }
        public string Status { get; set; } = string.Empty;
        public int VideosFound { get; set; }
        public int VideosNew { get; set; }
        public int SnapshotsWritten { get; set; }
        public int QuotaUnitsUsed { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? FinishedAt { get; set; }
    }

    /// <summary>Kết quả khi admin kích hoạt thu thập thủ công (job chạy nền qua Hangfire).</summary>
    public class TrendCollectionEnqueuedDto
    {
        public string JobId { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public string? Category { get; set; }
    }
}
