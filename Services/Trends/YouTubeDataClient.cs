using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PostFlow.Configuration;
using PostFlow.Services.Interfaces;

namespace PostFlow.Services.Trends
{
    public sealed record YouTubeSearchQuery(
        string RegionCode,
        string RelevanceLanguage,
        string? CategoryId,
        DateTime PublishedAfter,
        DateTime? PublishedBefore,
        string Order,
        string? PageToken = null,
        int MaxResults = 50);

    /// <summary>Chi phí quota của từng loại lệnh (đơn vị theo bảng của Google).</summary>
    public static class YouTubeQuotaCost
    {
        public const int Search = 100;
        public const int List = 1;
        public const int MaxIdsPerList = 50;
    }

    public class YouTubeApiException : Exception
    {
        public HttpStatusCode StatusCode { get; }
        public string? Reason { get; }

        public YouTubeApiException(HttpStatusCode statusCode, string? reason, string message)
            : base($"YouTube API {(int)statusCode} ({reason ?? "unknown"}): {message}")
        {
            StatusCode = statusCode;
            Reason = reason;
        }

        /// <summary>Hết quota ngày: không retry, dừng thu thập tới khi quota reset.</summary>
        public bool IsQuotaExceeded => Reason is "quotaExceeded" or "dailyLimitExceeded";
    }

    /// <summary>
    /// Client tối giản cho YouTube Data API v3 dùng API key. Key được gửi qua header x-goog-api-key
    /// để không xuất hiện trên URL (và do đó không lọt vào log).
    /// </summary>
    public class YouTubeDataClient : IYouTubeDataClient
    {
        public const string HttpClientName = "youtube-data";
        private const string BaseUrl = "https://www.googleapis.com/youtube/v3/";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<YouTubeDataClient> _logger;
        private readonly string? _apiKey;

        public YouTubeDataClient(
            IHttpClientFactory httpClientFactory,
            IOptions<PlatformSettings> platformSettings,
            ILogger<YouTubeDataClient> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;

            var configured = platformSettings.Value?.YouTube?.ApiKey;
            _apiKey = !string.IsNullOrWhiteSpace(configured)
                ? configured.Trim()
                : Environment.GetEnvironmentVariable("YOUTUBE_API_KEY")?.Trim();
        }

        public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

        public async Task<YouTubeSearchPage> SearchShortVideoIdsAsync(YouTubeSearchQuery query, CancellationToken cancellationToken = default)
        {
            var parameters = new Dictionary<string, string?>
            {
                ["part"] = "snippet",
                ["type"] = "video",
                ["videoDuration"] = "short",
                ["order"] = query.Order,
                ["regionCode"] = query.RegionCode,
                ["relevanceLanguage"] = query.RelevanceLanguage,
                ["videoCategoryId"] = query.CategoryId,
                ["publishedAfter"] = Rfc3339(query.PublishedAfter),
                ["publishedBefore"] = query.PublishedBefore.HasValue ? Rfc3339(query.PublishedBefore.Value) : null,
                ["maxResults"] = Math.Clamp(query.MaxResults, 1, 50).ToString(CultureInfo.InvariantCulture),
                ["pageToken"] = query.PageToken,
                ["fields"] = "nextPageToken,items(id(videoId))"
            };

            var json = await GetAsync("search", parameters, cancellationToken);
            return YouTubeResponseParser.ParseSearch(json);
        }

        public async Task<IReadOnlyList<YouTubeVideoItem>> GetVideosAsync(IReadOnlyCollection<string> videoIds, CancellationToken cancellationToken = default)
        {
            var ids = NormalizeIds(videoIds);
            if (ids.Count == 0) return Array.Empty<YouTubeVideoItem>();

            var json = await GetAsync("videos", new Dictionary<string, string?>
            {
                ["part"] = "snippet,contentDetails,statistics",
                ["id"] = string.Join(",", ids),
                ["maxResults"] = "50"
            }, cancellationToken);

            return YouTubeResponseParser.ParseVideos(json);
        }

        public async Task<IReadOnlyList<YouTubeVideoItem>> GetMostPopularAsync(string regionCode, string? categoryId, int maxResults, CancellationToken cancellationToken = default)
        {
            try
            {
                var json = await GetAsync("videos", new Dictionary<string, string?>
                {
                    ["part"] = "snippet,contentDetails,statistics",
                    ["chart"] = "mostPopular",
                    ["regionCode"] = regionCode,
                    ["videoCategoryId"] = categoryId,
                    ["maxResults"] = Math.Clamp(maxResults, 1, 50).ToString(CultureInfo.InvariantCulture)
                }, cancellationToken);

                return YouTubeResponseParser.ParseVideos(json);
            }
            catch (YouTubeApiException ex) when (ex.Reason == "videoChartNotFound")
            {
                // Một số danh mục không có chart thịnh hành ở một số vùng: đây không phải lỗi hệ thống.
                _logger.LogInformation("YouTube không có chart mostPopular cho danh mục {CategoryId} tại {Region}.", categoryId, regionCode);
                return Array.Empty<YouTubeVideoItem>();
            }
        }

        public async Task<IReadOnlyList<YouTubeChannelItem>> GetChannelsAsync(IReadOnlyCollection<string> channelIds, CancellationToken cancellationToken = default)
        {
            var ids = NormalizeIds(channelIds);
            if (ids.Count == 0) return Array.Empty<YouTubeChannelItem>();

            var json = await GetAsync("channels", new Dictionary<string, string?>
            {
                ["part"] = "snippet,statistics",
                ["id"] = string.Join(",", ids),
                ["maxResults"] = "50"
            }, cancellationToken);

            return YouTubeResponseParser.ParseChannels(json);
        }

        private async Task<string> GetAsync(string resource, IDictionary<string, string?> parameters, CancellationToken cancellationToken)
        {
            if (!IsConfigured)
            {
                throw new InvalidOperationException("Chưa cấu hình YouTube API key (PlatformSettings:YouTube:ApiKey hoặc biến môi trường YOUTUBE_API_KEY).");
            }

            var query = string.Join("&", parameters
                .Where(p => !string.IsNullOrWhiteSpace(p.Value))
                .Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value!)}"));

            using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}{resource}?{query}");
            request.Headers.Add("x-goog-api-key", _apiKey);

            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = YouTubeResponseParser.ParseError(body);
                _logger.LogWarning("YouTube API {Resource} trả về {Status} ({Reason}): {Message}",
                    resource, (int)response.StatusCode, error.Reason ?? "unknown", error.Message);
                throw new YouTubeApiException(response.StatusCode, error.Reason, error.Message);
            }

            return body;
        }

        private static List<string> NormalizeIds(IReadOnlyCollection<string> ids)
        {
            var normalized = ids.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).Distinct().ToList();
            if (normalized.Count > YouTubeQuotaCost.MaxIdsPerList)
            {
                throw new ArgumentException($"Mỗi lệnh list chỉ nhận tối đa {YouTubeQuotaCost.MaxIdsPerList} id.", nameof(ids));
            }
            return normalized;
        }

        private static string Rfc3339(DateTime value) =>
            DateTime.SpecifyKind(value.ToUniversalTime(), DateTimeKind.Utc).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
    }
}
