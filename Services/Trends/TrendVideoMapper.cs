using System.Collections.Generic;
using System.Linq;
using PostFlow.Models;
using PostFlow.Models.DTOs.Responses;

namespace PostFlow.Services.Trends
{
    /// <summary>Chuyển entity benchmark sang DTO và tính các chỉ số suy ra tại thời điểm đọc.</summary>
    public static class TrendVideoMapper
    {
        public static class SampleNames
        {
            public const string Popular = "popular";
            public const string Viral = "viral";
            public const string Control = "control";
            public const string Manual = "manual";
        }

        /// <param name="previousSnapshot">Snapshot liền trước snapshot mới nhất, dùng để tính tốc độ tăng.</param>
        public static TrendTopicDto ToTopicDto(this TrendVideo video, TrendVideoSnapshot? previousSnapshot, int snapshotCount, int? viralityScore)
        {
            var measuredAt = video.LastSnapshotAt ?? video.FirstSeenAt;
            var ageHours = TrendMetrics.AgeHours(video.PublishedAt, measuredAt);
            var subscribers = video.Channel?.SubscriberCount;
            var growth = previousSnapshot == null || video.LastSnapshotAt == null
                ? null
                : TrendMetrics.Growth24h(previousSnapshot.Views, previousSnapshot.CapturedAt, video.LatestViews, video.LastSnapshotAt.Value);

            return new TrendTopicDto
            {
                Id = video.Id,
                ExternalId = video.ExternalId,
                Title = video.Title,
                Platform = video.Platform,
                Category = video.CategorySlug,
                VideoUrl = video.Url,
                EmbedUrl = video.EmbedUrl,
                AuthorName = video.Channel?.Title is { Length: > 0 } channelTitle ? channelTitle : video.ChannelTitle,
                AuthorAvatarUrl = video.Channel?.AvatarUrl ?? string.Empty,
                ThumbnailUrl = video.ThumbnailUrl,
                Description = video.Description.Length > 300 ? video.Description[..300] + "..." : video.Description,
                Hashtags = video.Tags.Take(6).Select(t => t.StartsWith('#') ? t : "#" + t.Replace(" ", "")).ToList(),

                ViewsCount = TrendMetrics.FormatCount(video.LatestViews),
                LikesCount = TrendMetrics.FormatCount(video.LatestLikes),
                Duration = TrendMetrics.FormatDuration(video.DurationSeconds),
                GrowthRate = TrendMetrics.FormatGrowth(growth),

                Views = video.LatestViews,
                Likes = video.LatestLikes,
                Comments = video.LatestComments,
                SubscriberCount = subscribers,
                DurationSeconds = video.DurationSeconds,
                IsShort = video.IsShort,

                AgeHours = ageHours,
                ViewsPerHour = TrendMetrics.ViewsPerHour(video.LatestViews, ageHours),
                EngagementRate = TrendMetrics.EngagementRate(video.LatestViews, video.LatestLikes, video.LatestComments),
                ViewsPerSubscriber = TrendMetrics.ViewsPerSubscriber(video.LatestViews, subscribers),
                Growth24h = growth,
                ViralityScore = viralityScore,

                SampleTypes = SampleTypes(video),
                DataSource = video.DataSource,
                SnapshotCount = snapshotCount,
                PublishedAt = video.PublishedAt,
                FirstSeenAt = video.FirstSeenAt,
                LastUpdatedAt = video.LastSnapshotAt,
                TrendingSince = video.PublishedAt ?? video.FirstSeenAt
            };
        }

        public static TrendSnapshotDto ToDto(this TrendVideoSnapshot snapshot) => new()
        {
            CapturedAt = snapshot.CapturedAt,
            AgeHours = snapshot.AgeHours,
            MilestoneHours = snapshot.MilestoneHours,
            Views = snapshot.Views,
            Likes = snapshot.Likes,
            Comments = snapshot.Comments,
            DataSource = snapshot.DataSource
        };

        public static TrendCollectionRunDto ToDto(this TrendCollectionRun run) => new()
        {
            Kind = run.Kind,
            Source = run.Source,
            Category = run.CategorySlug,
            Status = run.Status,
            VideosFound = run.VideosFound,
            VideosNew = run.VideosNew,
            SnapshotsWritten = run.SnapshotsWritten,
            QuotaUnitsUsed = run.QuotaUnitsUsed,
            ErrorMessage = run.ErrorMessage,
            StartedAt = run.StartedAt,
            FinishedAt = run.FinishedAt
        };

        public static List<string> SampleTypes(TrendVideo video)
        {
            var types = new List<string>();
            if (video.InPopularChart) types.Add(SampleNames.Popular);
            if (video.InViralSample) types.Add(SampleNames.Viral);
            if (video.InControlSample) types.Add(SampleNames.Control);
            if (video.SubmittedByUserId != null || video.DataSource != TrendDataSources.YouTubeApi) types.Add(SampleNames.Manual);
            return types;
        }
    }
}
