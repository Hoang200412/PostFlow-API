using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PostFlow.Models;

namespace PostFlow.Services.Trends
{
    public static class TrendSnapshots
    {
        /// <summary>Tạo snapshot mới cho video và cập nhật số liệu mới nhất lưu đúp trên video. Caller tự Add vào DbContext.</summary>
        public static TrendVideoSnapshot Record(
            TrendVideo video, long? views, long? likes, long? comments, DateTime capturedAt, int? milestoneHours, string dataSource)
        {
            video.LatestViews = views;
            video.LatestLikes = likes;
            video.LatestComments = comments;
            video.LastSnapshotAt = capturedAt;

            return new TrendVideoSnapshot
            {
                TrendVideoId = video.Id,
                CapturedAt = capturedAt,
                AgeHours = TrendMetrics.AgeHours(video.PublishedAt, capturedAt),
                MilestoneHours = milestoneHours,
                Views = views,
                Likes = likes,
                Comments = comments,
                DataSource = dataSource
            };
        }
    }

    /// <summary>
    /// Nhận diện YouTube Shorts. API không có cờ "isShort" nên đây là heuristic, cần ghi rõ trong báo cáo:
    /// video ≤ 60s, hoặc ≤ ngưỡng tối đa (mặc định 180s) kèm dấu hiệu #shorts trong tiêu đề/mô tả/tags.
    /// </summary>
    public static class ShortsClassifier
    {
        public const int ClassicShortsSeconds = 60;

        public static bool IsShort(int? durationSeconds, string? title, string? description, IEnumerable<string>? tags, int maxSeconds)
        {
            if (durationSeconds is not { } seconds || seconds <= 0) return false;
            if (seconds > maxSeconds) return false;
            if (seconds <= ClassicShortsSeconds) return true;

            return ContainsShortsMarker(title)
                || ContainsShortsMarker(description)
                || (tags?.Any(tag => tag.Trim().TrimStart('#').Equals("shorts", StringComparison.OrdinalIgnoreCase)) ?? false);
        }

        private static bool ContainsShortsMarker(string? text) =>
            !string.IsNullOrEmpty(text) && text.Contains("#shorts", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Quyết định snapshot đang đến hạn ở mốc tuổi nào. Mỗi mốc có một khoảng dung sai; trễ quá dung sai thì bỏ mốc đó
    /// thay vì ghi một số đo sai tuổi vào mốc chuẩn (để so sánh "views sau 24h" giữa các video là công bằng).
    /// </summary>
    public static class SnapshotPlanner
    {
        public static double ToleranceHours(int milestoneHours) => Math.Max(6.0, milestoneHours * 0.25);

        public static int? DueMilestone(double ageHours, IEnumerable<int> recordedMilestones, IEnumerable<int> milestones)
        {
            var recorded = recordedMilestones as ISet<int> ?? recordedMilestones.ToHashSet();

            foreach (var milestone in milestones.Distinct().OrderBy(m => m))
            {
                if (recorded.Contains(milestone)) continue;
                if (ageHours < milestone) return null;
                if (ageHours <= milestone + ToleranceHours(milestone)) return milestone;
                // Đã lỡ mốc này: xét mốc tiếp theo.
            }

            return null;
        }

        /// <summary>Ngừng theo dõi khi video đã quá tuổi theo dõi và đã qua mốc cuối cùng.</summary>
        public static bool ShouldStopTracking(double ageHours, IEnumerable<int> milestones, int trackingDays)
        {
            var list = milestones.ToList();
            var lastWindowEnd = list.Count == 0 ? 0 : list.Max(m => m + ToleranceHours(m));
            return ageHours > Math.Max(trackingDays * 24.0, lastWindowEnd);
        }
    }

    /// <summary>
    /// Cửa sổ thời gian cho mẫu đối chứng. Search sắp theo ngày đăng trả các video mới nhất trước mốc publishedBefore,
    /// nên mỗi ngày dịch mốc này đi một ngày để dần phủ toàn bộ khoảng LookbackDays, cho ra mẫu cùng độ tuổi với nhóm viral.
    /// </summary>
    public static class ControlSampleWindow
    {
        public static (DateTime PublishedAfter, DateTime PublishedBefore) For(DateTime utcNow, int lookbackDays)
        {
            var lookback = Math.Max(2, lookbackDays);
            var dayIndex = (int)(utcNow.Date - DateTime.UnixEpoch.Date).TotalDays;
            var endOffsetDays = 1 + (dayIndex % (lookback - 1));

            return (utcNow.AddDays(-lookback), utcNow.AddDays(-endOffsetDays));
        }
    }

    /// <summary>Quota YouTube reset lúc 0h giờ Pacific. Tính thời điểm bắt đầu "ngày quota" hiện tại theo UTC.</summary>
    public static class YouTubeQuotaDay
    {
        private static readonly TimeZoneInfo Pacific = ResolvePacific();

        public static DateTime StartUtc(DateTime utcNow)
        {
            var utc = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
            var local = TimeZoneInfo.ConvertTimeFromUtc(utc, Pacific);
            var localMidnight = DateTime.SpecifyKind(local.Date, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTimeToUtc(localMidnight, Pacific);
        }

        private static TimeZoneInfo ResolvePacific()
        {
            foreach (var id in new[] { "America/Los_Angeles", "Pacific Standard Time" })
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
                catch (TimeZoneNotFoundException) { }
                catch (InvalidTimeZoneException) { }
            }
            return TimeZoneInfo.CreateCustomTimeZone("PST-fallback", TimeSpan.FromHours(-8), "PST", "PST");
        }
    }

    /// <summary>Các chỉ số suy ra từ số liệu thô. Thiếu dữ liệu thì trả null, không đoán.</summary>
    public static class TrendMetrics
    {
        /// <summary>Khoảng tối thiểu giữa hai snapshot để tính tốc độ tăng, tránh phóng đại từ khoảng đo quá ngắn.</summary>
        public const double MinGrowthIntervalHours = 6.0;

        public static double? AgeHours(DateTime? publishedAt, DateTime at) =>
            publishedAt.HasValue ? Math.Max(0, (at - publishedAt.Value).TotalHours) : null;

        public static double? ViewsPerHour(long? views, double? ageHours) =>
            views.HasValue && ageHours.HasValue ? views.Value / Math.Max(1.0, ageHours.Value) : null;

        public static double? EngagementRate(long? views, long? likes, long? comments)
        {
            if (views is not > 0 || (likes is null && comments is null)) return null;
            return ((likes ?? 0) + (comments ?? 0)) / (double)views.Value;
        }

        public static double? ViewsPerSubscriber(long? views, long? subscribers) =>
            views.HasValue && subscribers is > 0 ? views.Value / (double)subscribers.Value : null;

        /// <summary>Tốc độ tăng views quy về 24 giờ (tăng trưởng kép) giữa hai lần đo.</summary>
        public static double? Growth24h(long? previousViews, DateTime previousAt, long? currentViews, DateTime currentAt)
        {
            if (previousViews is not > 0 || currentViews is null) return null;
            var hours = (currentAt - previousAt).TotalHours;
            if (hours < MinGrowthIntervalHours) return null;

            var ratio = currentViews.Value / (double)previousViews.Value;
            return Math.Pow(ratio, 24.0 / hours) - 1.0;
        }

        /// <summary>Thứ hạng phần trăm (0-100) của value trong population. Null khi quần thể quá nhỏ để có nghĩa.</summary>
        public static int? PercentileRank(double? value, IReadOnlyCollection<double> population, int minPopulation = 5)
        {
            if (value is null || population.Count < minPopulation) return null;
            var below = population.Count(p => p < value.Value);
            var equal = population.Count(p => p == value.Value);
            var rank = (below + 0.5 * equal) / population.Count;
            return (int)Math.Round(rank * 100);
        }

        public static string FormatCount(long? value)
        {
            if (value is null) return string.Empty;
            var v = value.Value;
            // Định dạng cố định theo InvariantCulture để "1.5K" không thành "1,5K" trên máy chủ locale vi-VN.
            return v switch
            {
                >= 1_000_000_000 => string.Create(CultureInfo.InvariantCulture, $"{v / 1_000_000_000.0:0.#}B"),
                >= 1_000_000 => string.Create(CultureInfo.InvariantCulture, $"{v / 1_000_000.0:0.#}M"),
                >= 1_000 => string.Create(CultureInfo.InvariantCulture, $"{v / 1_000.0:0.#}K"),
                _ => v.ToString(CultureInfo.InvariantCulture)
            };
        }

        public static string FormatDuration(int? seconds)
        {
            if (seconds is null) return string.Empty;
            var span = TimeSpan.FromSeconds(seconds.Value);
            return span.TotalHours >= 1
                ? $"{(int)span.TotalHours}:{span.Minutes:D2}:{span.Seconds:D2}"
                : $"{(int)span.TotalMinutes}:{span.Seconds:D2}";
        }

        public static string FormatGrowth(double? growth24h) =>
            growth24h is null
                ? string.Empty
                : string.Create(CultureInfo.InvariantCulture, $"{(growth24h.Value >= 0 ? "+" : "")}{growth24h.Value * 100:0.#}%/24h");
    }
}
