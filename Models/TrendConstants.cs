namespace PostFlow.Models
{
    /// <summary>Nguồn gốc của một dòng dữ liệu xu hướng. Mọi dòng đều phải có nguồn có thể kiểm chứng.</summary>
    public static class TrendDataSources
    {
        /// <summary>Lấy trực tiếp từ YouTube Data API v3.</summary>
        public const string YouTubeApi = "youtube_api";

        /// <summary>Metadata lấy từ oEmbed công khai của nền tảng.</summary>
        public const string OEmbed = "oembed";

        /// <summary>Do người dùng nhập tay (ví dụ số views TikTok/Reels).</summary>
        public const string Manual = "manual";
    }

    public static class TrendPlatforms
    {
        public const string YouTube = "youtube";
        public const string TikTok = "tiktok";
        public const string Reels = "reels";

        public static readonly string[] All = { YouTube, TikTok, Reels };
    }

    public static class TrendRunStatuses
    {
        public const string Succeeded = "Succeeded";
        public const string Failed = "Failed";
        public const string QuotaExceeded = "QuotaExceeded";
        public const string BudgetExhausted = "BudgetExhausted";
        public const string Skipped = "Skipped";
    }
}
