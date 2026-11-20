using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using PostFlow.Models;

namespace PostFlow.Services.Trends
{
    public sealed record ParsedTrendUrl(string Platform, string? ExternalId, string CanonicalUrl);

    /// <summary>Nhận diện nền tảng và id video từ URL do người dùng dán vào.</summary>
    public static class TrendUrlParser
    {
        private static readonly Regex YouTubeId = new("^[A-Za-z0-9_-]{11}$", RegexOptions.Compiled);
        private static readonly Regex TikTokVideo = new(@"/video/(\d{8,25})", RegexOptions.Compiled);
        private static readonly Regex FacebookReel = new(@"/reel/(\d{5,25})", RegexOptions.Compiled);
        private static readonly Regex FacebookVideo = new(@"/videos/(?:[^/]+/)?(\d{5,25})", RegexOptions.Compiled);
        private static readonly Regex InstagramReel = new(@"/(?:reel|reels|p)/([A-Za-z0-9_-]{5,40})", RegexOptions.Compiled);

        public static ParsedTrendUrl? Parse(string? rawUrl)
        {
            if (string.IsNullOrWhiteSpace(rawUrl)) return null;
            var text = rawUrl.Trim();
            if (!text.StartsWith("http", StringComparison.OrdinalIgnoreCase)) text = "https://" + text;
            if (!Uri.TryCreate(text, UriKind.Absolute, out var uri)) return null;
            if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) return null;

            var host = uri.Host.ToLowerInvariant();
            if (host.StartsWith("www.")) host = host[4..];
            if (host.StartsWith("m.")) host = host[2..];
            var path = uri.AbsolutePath;

            if (host is "youtube.com" or "music.youtube.com" or "youtu.be")
            {
                var id = ExtractYouTubeId(uri, host);
                return id == null ? null : new ParsedTrendUrl(TrendPlatforms.YouTube, id, $"https://www.youtube.com/watch?v={id}");
            }

            if (host.EndsWith("tiktok.com"))
            {
                var match = TikTokVideo.Match(path);
                // Link rút gọn (vm.tiktok.com/...) không chứa id: oEmbed sẽ trả embed_product_id.
                return new ParsedTrendUrl(TrendPlatforms.TikTok, match.Success ? match.Groups[1].Value : null, StripQuery(uri));
            }

            if (host is "facebook.com" or "fb.watch" or "web.facebook.com")
            {
                var match = FacebookReel.Match(path);
                if (!match.Success) match = FacebookVideo.Match(path);
                if (match.Success)
                {
                    return new ParsedTrendUrl(TrendPlatforms.Reels, match.Groups[1].Value, StripQuery(uri));
                }

                // Dạng facebook.com/watch/?v=ID: id nằm trong query nên không được bỏ query khi chuẩn hóa.
                var watchId = HttpUtility.ParseQueryString(uri.Query).Get("v");
                if (!string.IsNullOrWhiteSpace(watchId) && Regex.IsMatch(watchId, @"^\d{5,25}$"))
                {
                    return new ParsedTrendUrl(TrendPlatforms.Reels, watchId, $"https://www.facebook.com/watch/?v={watchId}");
                }

                // Link rút gọn fb.watch không chứa id: giữ nguyên URL, id sẽ được băm từ URL.
                return new ParsedTrendUrl(TrendPlatforms.Reels, null, uri.ToString());
            }

            if (host == "instagram.com")
            {
                var match = InstagramReel.Match(path);
                return match.Success
                    ? new ParsedTrendUrl(TrendPlatforms.Reels, "ig_" + match.Groups[1].Value, $"https://www.instagram.com/reel/{match.Groups[1].Value}/")
                    : null;
            }

            return null;
        }

        /// <summary>Id ổn định cho URL không chứa id (ví dụ fb.watch): băm URL chuẩn hóa.</summary>
        public static string HashId(string canonicalUrl)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalUrl.Trim().ToLowerInvariant()));
            return "url_" + Convert.ToHexString(bytes, 0, 8).ToLowerInvariant();
        }

        /// <summary>
        /// Id video TikTok mã hóa thời điểm đăng ở 32 bit cao (unix seconds). Trả null nếu kết quả không hợp lý.
        /// </summary>
        public static DateTime? TikTokPublishedAt(string? videoId, DateTime utcNow)
        {
            if (!ulong.TryParse(videoId, out var id)) return null;
            var seconds = (long)(id >> 32);
            if (seconds <= 0) return null;

            var publishedAt = DateTime.UnixEpoch.AddSeconds(seconds);
            return publishedAt.Year >= 2016 && publishedAt <= utcNow.AddDays(1) ? publishedAt : null;
        }

        private static string? ExtractYouTubeId(Uri uri, string host)
        {
            string? candidate = null;
            var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (host == "youtu.be")
            {
                candidate = segments.Length > 0 ? segments[0] : null;
            }
            else if (segments.Length >= 2 && segments[0] is "shorts" or "embed" or "live" or "v")
            {
                candidate = segments[1];
            }
            else
            {
                candidate = HttpUtility.ParseQueryString(uri.Query).Get("v");
            }

            return candidate != null && YouTubeId.IsMatch(candidate) ? candidate : null;
        }

        private static string StripQuery(Uri uri) => $"https://{uri.Host.ToLowerInvariant()}{uri.AbsolutePath.TrimEnd('/')}";
    }
}
