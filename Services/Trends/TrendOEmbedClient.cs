using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PostFlow.Configuration;
using PostFlow.Services.Interfaces;

namespace PostFlow.Services.Trends
{
    /// <summary>Metadata công khai lấy qua oEmbed. oEmbed không cung cấp số views/likes.</summary>
    public sealed record OEmbedInfo(
        string? Title,
        string? AuthorName,
        string? AuthorId,
        string? ThumbnailUrl,
        string? VideoId);

    public class TrendOEmbedClient : ITrendOEmbedClient
    {
        public const string HttpClientName = "trend-oembed";
        private const string GraphVersion = "v21.0";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly FacebookReelsSettings _facebook;
        private readonly ILogger<TrendOEmbedClient> _logger;

        public TrendOEmbedClient(
            IHttpClientFactory httpClientFactory,
            IOptions<PlatformSettings> platformSettings,
            ILogger<TrendOEmbedClient> logger)
        {
            _httpClientFactory = httpClientFactory;
            _facebook = platformSettings.Value?.FacebookReels ?? new FacebookReelsSettings();
            _logger = logger;
        }

        public async Task<OEmbedInfo?> GetTikTokAsync(string url, CancellationToken cancellationToken = default)
        {
            var json = await GetJsonAsync($"https://www.tiktok.com/oembed?url={Uri.EscapeDataString(url)}", "TikTok", cancellationToken);
            return json == null ? null : ParseTikTok(json);
        }

        public async Task<OEmbedInfo?> GetFacebookVideoAsync(string url, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_facebook.AppId) || string.IsNullOrWhiteSpace(_facebook.AppSecret)) return null;

            var token = Uri.EscapeDataString($"{_facebook.AppId}|{_facebook.AppSecret}");
            var endpoint = $"https://graph.facebook.com/{GraphVersion}/oembed_video?url={Uri.EscapeDataString(url)}&omitscript=true&access_token={token}";
            var json = await GetJsonAsync(endpoint, "Facebook", cancellationToken);
            return json == null ? null : ParseFacebook(json);
        }

        internal static OEmbedInfo? ParseTikTok(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                return new OEmbedInfo(
                    Title: Get(root, "title"),
                    AuthorName: Get(root, "author_name"),
                    AuthorId: Get(root, "author_unique_id"),
                    ThumbnailUrl: Get(root, "thumbnail_url"),
                    VideoId: Get(root, "embed_product_id"));
            }
            catch (JsonException)
            {
                return null;
            }
        }

        internal static OEmbedInfo? ParseFacebook(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("error", out _)) return null;
                return new OEmbedInfo(
                    Title: Get(root, "title"),
                    AuthorName: Get(root, "author_name"),
                    AuthorId: null,
                    ThumbnailUrl: Get(root, "thumbnail_url"),
                    VideoId: null);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private async Task<string?> GetJsonAsync(string url, string provider, CancellationToken cancellationToken)
        {
            try
            {
                var client = _httpClientFactory.CreateClient(HttpClientName);
                using var response = await client.GetAsync(url, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    // Không log URL vì URL Facebook chứa access token.
                    _logger.LogWarning("{Provider} oEmbed trả về {Status}.", provider, (int)response.StatusCode);
                    return null;
                }
                return await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogWarning("{Provider} oEmbed lỗi kết nối: {Message}", provider, ex.Message);
                return null;
            }
        }

        private static string? Get(JsonElement element, string name) =>
            element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
    }
}
