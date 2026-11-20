using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PostFlow.Configuration;
using PostFlow.Data;
using PostFlow.Exceptions;
using PostFlow.Models;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Services.Interfaces;

namespace PostFlow.Services.Trends
{
    public class TrendManualImporter : ITrendManualImporter
    {
        private static readonly Regex Hashtag = new(@"#[\p{L}\p{N}_]+", RegexOptions.Compiled);

        private readonly AppDbContext _context;
        private readonly ITrendCollector _collector;
        private readonly ITrendOEmbedClient _oEmbed;
        private readonly TrendCollectionSettings _settings;
        private readonly TimeProvider _time;

        public TrendManualImporter(
            AppDbContext context,
            ITrendCollector collector,
            ITrendOEmbedClient oEmbed,
            IOptions<TrendCollectionSettings> settings,
            TimeProvider time)
        {
            _context = context;
            _collector = collector;
            _oEmbed = oEmbed;
            _settings = settings.Value;
            _time = time;
        }

        public async Task<TrendVideo> ImportAsync(string userId, ManualTrendImportRequest request, CancellationToken cancellationToken = default)
        {
            var category = request.Category?.Trim().ToLowerInvariant() ?? string.Empty;
            if (!_settings.CategoryMap.ContainsKey(category))
            {
                throw new BadRequestException($"Danh mục không hợp lệ. Chọn một trong: {string.Join(", ", _settings.CategoryMap.Keys)}.");
            }

            var parsed = TrendUrlParser.Parse(request.Url)
                ?? throw new BadRequestException("Link không hợp lệ hoặc chưa hỗ trợ. Hỗ trợ YouTube, TikTok, Facebook Reels và Instagram Reels.");

            if (parsed.Platform == TrendPlatforms.YouTube)
            {
                return await _collector.ImportYouTubeVideoAsync(parsed.ExternalId!, category, userId, cancellationToken);
            }

            var now = _time.GetUtcNow().UtcDateTime;
            var isInstagram = parsed.ExternalId?.StartsWith("ig_") == true;

            OEmbedInfo? info = parsed.Platform switch
            {
                TrendPlatforms.TikTok => await _oEmbed.GetTikTokAsync(parsed.CanonicalUrl, cancellationToken),
                TrendPlatforms.Reels when !isInstagram => await _oEmbed.GetFacebookVideoAsync(parsed.CanonicalUrl, cancellationToken),
                _ => null
            };

            var externalId = parsed.ExternalId ?? info?.VideoId ?? TrendUrlParser.HashId(parsed.CanonicalUrl);
            var title = FirstNonEmpty(info?.Title, request.Title)
                ?? throw new BadRequestException("Không lấy được tiêu đề tự động từ nền tảng, vui lòng nhập tiêu đề video.");

            var video = await _context.TrendVideos
                .FirstOrDefaultAsync(v => v.Platform == parsed.Platform && v.ExternalId == externalId, cancellationToken);

            if (video == null)
            {
                video = new TrendVideo
                {
                    Platform = parsed.Platform,
                    ExternalId = externalId,
                    CategorySlug = category,
                    DataSource = info != null ? TrendDataSources.OEmbed : TrendDataSources.Manual,
                    SubmittedByUserId = userId,
                    IsShort = true,
                    // Không có API để đo lại tự động: job snapshot không theo dõi video nhập tay.
                    IsTracking = false,
                    FirstSeenAt = now
                };
                _context.TrendVideos.Add(video);
            }

            video.Title = title.Length > 500 ? title[..500] : title;
            video.Url = parsed.CanonicalUrl;
            video.EmbedUrl = BuildEmbedUrl(parsed.Platform, externalId, parsed.CanonicalUrl);
            video.ThumbnailUrl = info?.ThumbnailUrl ?? video.ThumbnailUrl;
            video.ChannelTitle = info?.AuthorName ?? video.ChannelTitle;
            video.ChannelExternalId = info?.AuthorId ?? video.ChannelExternalId;
            video.Tags = Hashtag.Matches(title).Select(m => m.Value).Distinct().Take(10).ToList();
            video.PublishedAt = ResolvePublishedAt(request.PublishedAt, parsed.Platform, externalId, now) ?? video.PublishedAt;

            if (request.Views.HasValue || request.Likes.HasValue || request.Comments.HasValue)
            {
                _context.TrendVideoSnapshots.Add(TrendSnapshots.Record(
                    video, request.Views, request.Likes, request.Comments, now, milestoneHours: null, TrendDataSources.Manual));
            }

            await _context.SaveChangesAsync(cancellationToken);
            return video;
        }

        private static DateTime? ResolvePublishedAt(DateTime? requested, string platform, string externalId, DateTime now)
        {
            if (requested.HasValue)
            {
                var utc = requested.Value.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(requested.Value, DateTimeKind.Utc)
                    : requested.Value.ToUniversalTime();
                if (utc > now.AddHours(1)) throw new BadRequestException("Thời điểm đăng không được ở tương lai.");
                return utc;
            }

            return platform == TrendPlatforms.TikTok ? TrendUrlParser.TikTokPublishedAt(externalId, now) : null;
        }

        private static string BuildEmbedUrl(string platform, string externalId, string canonicalUrl)
        {
            if (platform == TrendPlatforms.TikTok)
            {
                return externalId.All(char.IsDigit) ? $"https://www.tiktok.com/embed/v2/{externalId}" : string.Empty;
            }

            if (externalId.StartsWith("ig_"))
            {
                return $"https://www.instagram.com/reel/{externalId[3..]}/embed";
            }

            return $"https://www.facebook.com/plugins/video.php?href={Uri.EscapeDataString(canonicalUrl)}&show_text=false";
        }

        private static string? FirstNonEmpty(params string?[] values) =>
            values.Select(v => v?.Trim()).FirstOrDefault(v => !string.IsNullOrEmpty(v));
    }
}
