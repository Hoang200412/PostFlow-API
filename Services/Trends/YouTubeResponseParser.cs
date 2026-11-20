using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Xml;

namespace PostFlow.Services.Trends
{
    public sealed record YouTubeVideoItem(
        string Id,
        string Title,
        string Description,
        string ChannelId,
        string ChannelTitle,
        DateTime? PublishedAt,
        int? DurationSeconds,
        string? CategoryId,
        string? Language,
        IReadOnlyList<string> Tags,
        string ThumbnailUrl,
        long? Views,
        long? Likes,
        long? Comments);

    public sealed record YouTubeChannelItem(string Id, string Title, long? SubscriberCount, long? VideoCount, string AvatarUrl = "");

    public sealed record YouTubeSearchPage(IReadOnlyList<string> VideoIds, string? NextPageToken);

    public sealed record YouTubeApiError(string? Reason, string Message);

    /// <summary>
    /// Đọc JSON trả về từ YouTube Data API v3. Không ném lỗi với field thiếu: field nào không có thì để null,
    /// tuyệt đối không điền giá trị mặc định giả.
    /// </summary>
    public static class YouTubeResponseParser
    {
        public static IReadOnlyList<YouTubeVideoItem> ParseVideos(string json)
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<YouTubeVideoItem>();
            }

            var result = new List<YouTubeVideoItem>();
            foreach (var item in items.EnumerateArray())
            {
                var id = GetString(item, "id");
                if (string.IsNullOrWhiteSpace(id)) continue;

                item.TryGetProperty("snippet", out var snippet);
                item.TryGetProperty("statistics", out var stats);
                item.TryGetProperty("contentDetails", out var details);

                result.Add(new YouTubeVideoItem(
                    Id: id,
                    Title: GetString(snippet, "title") ?? string.Empty,
                    Description: GetString(snippet, "description") ?? string.Empty,
                    ChannelId: GetString(snippet, "channelId") ?? string.Empty,
                    ChannelTitle: GetString(snippet, "channelTitle") ?? string.Empty,
                    PublishedAt: ParseTimestamp(GetString(snippet, "publishedAt")),
                    DurationSeconds: ParseIsoDurationSeconds(GetString(details, "duration")),
                    CategoryId: GetString(snippet, "categoryId"),
                    Language: GetString(snippet, "defaultAudioLanguage") ?? GetString(snippet, "defaultLanguage"),
                    Tags: GetStringArray(snippet, "tags"),
                    ThumbnailUrl: PickThumbnail(snippet),
                    Views: GetLong(stats, "viewCount"),
                    Likes: GetLong(stats, "likeCount"),
                    Comments: GetLong(stats, "commentCount")));
            }

            return result;
        }

        public static IReadOnlyList<YouTubeChannelItem> ParseChannels(string json)
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<YouTubeChannelItem>();
            }

            var result = new List<YouTubeChannelItem>();
            foreach (var item in items.EnumerateArray())
            {
                var id = GetString(item, "id");
                if (string.IsNullOrWhiteSpace(id)) continue;

                item.TryGetProperty("snippet", out var snippet);
                item.TryGetProperty("statistics", out var stats);

                var hidden = stats.ValueKind == JsonValueKind.Object
                    && stats.TryGetProperty("hiddenSubscriberCount", out var hiddenElem)
                    && hiddenElem.ValueKind == JsonValueKind.True;

                result.Add(new YouTubeChannelItem(
                    Id: id,
                    Title: GetString(snippet, "title") ?? string.Empty,
                    SubscriberCount: hidden ? null : GetLong(stats, "subscriberCount"),
                    VideoCount: GetLong(stats, "videoCount"),
                    AvatarUrl: PickThumbnail(snippet)));
            }

            return result;
        }

        public static YouTubeSearchPage ParseSearch(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var ids = new List<string>();

            if (doc.RootElement.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in items.EnumerateArray())
                {
                    if (item.TryGetProperty("id", out var idElem))
                    {
                        var videoId = GetString(idElem, "videoId");
                        if (!string.IsNullOrWhiteSpace(videoId) && !ids.Contains(videoId)) ids.Add(videoId);
                    }
                }
            }

            return new YouTubeSearchPage(ids, GetString(doc.RootElement, "nextPageToken"));
        }

        /// <summary>Đọc lỗi chuẩn của Google API: { "error": { "message", "errors": [ { "reason" } ] } }.</summary>
        public static YouTubeApiError ParseError(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new YouTubeApiError(null, "Phản hồi lỗi rỗng.");

            try
            {
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("error", out var error)) return new YouTubeApiError(null, Truncate(json));

                string? reason = null;
                if (error.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0)
                {
                    reason = GetString(errors[0], "reason");
                }

                return new YouTubeApiError(reason, GetString(error, "message") ?? "Lỗi không rõ từ YouTube API.");
            }
            catch (JsonException)
            {
                return new YouTubeApiError(null, Truncate(json));
            }
        }

        /// <summary>Đổi ISO 8601 duration (PT1M5S) sang giây. Trả null nếu không đọc được, không đoán.</summary>
        public static int? ParseIsoDurationSeconds(string? iso)
        {
            if (string.IsNullOrWhiteSpace(iso)) return null;
            try
            {
                var span = XmlConvert.ToTimeSpan(iso);
                return span.TotalSeconds < 0 ? null : (int)Math.Round(span.TotalSeconds);
            }
            catch (FormatException)
            {
                return null;
            }
        }

        private static DateTime? ParseTimestamp(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            return DateTime.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed)
                ? DateTime.SpecifyKind(parsed, DateTimeKind.Utc)
                : null;
        }

        private static string PickThumbnail(JsonElement snippet)
        {
            if (snippet.ValueKind != JsonValueKind.Object || !snippet.TryGetProperty("thumbnails", out var thumbs)) return string.Empty;

            foreach (var size in new[] { "high", "medium", "standard", "default" })
            {
                if (thumbs.TryGetProperty(size, out var thumb))
                {
                    var url = GetString(thumb, "url");
                    if (!string.IsNullOrWhiteSpace(url)) return url;
                }
            }
            return string.Empty;
        }

        private static string? GetString(JsonElement element, string name)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value)) return null;
            return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        }

        /// <summary>YouTube trả số dưới dạng chuỗi ("12345"). Field vắng mặt (ẩn like, tắt comment) → null.</summary>
        private static long? GetLong(JsonElement element, string name)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value)) return null;
            return value.ValueKind switch
            {
                JsonValueKind.String when long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
                JsonValueKind.Number when value.TryGetInt64(out var number) => number,
                _ => null
            };
        }

        private static IReadOnlyList<string> GetStringArray(JsonElement element, string name)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<string>();
            }

            return value.EnumerateArray()
                .Where(v => v.ValueKind == JsonValueKind.String)
                .Select(v => v.GetString()!.Trim())
                .Where(v => v.Length > 0)
                .ToList();
        }

        private static string Truncate(string text) => text.Length > 300 ? text[..300] : text;
    }
}
