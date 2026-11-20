using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PostFlow.Configuration;
using PostFlow.Data;
using PostFlow.Exceptions;
using PostFlow.Models;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Models.DTOs.Responses;
using PostFlow.Services.Interfaces;
using PostFlow.Services.Trends;

namespace PostFlow.Services
{
    public class TrendService : ITrendService
    {
        /// <summary>Số video tối đa nạp để xếp hạng trong một request feed.</summary>
        private const int MaxCandidates = 2000;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private static readonly string[] SampleFilters = { "trending", "popular", "viral", "control", "manual", "all" };

        private readonly AppDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ITrendCollector _collector;
        private readonly ITrendManualImporter _manualImporter;
        private readonly IYouTubeDataClient _youTube;
        private readonly IGeminiClient _geminiClient;
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly AISettings _aiSettings;
        private readonly TrendCollectionSettings _trendSettings;
        private readonly TimeProvider _time;
        private readonly ILogger<TrendService> _logger;

        public TrendService(
            AppDbContext context,
            IHttpClientFactory httpClientFactory,
            ITrendCollector collector,
            ITrendManualImporter manualImporter,
            IYouTubeDataClient youTube,
            IGeminiClient geminiClient,
            IBackgroundJobClient backgroundJobClient,
            IOptions<AISettings> aiSettings,
            IOptions<TrendCollectionSettings> trendSettings,
            TimeProvider time,
            ILogger<TrendService> logger)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _collector = collector;
            _manualImporter = manualImporter;
            _youTube = youTube;
            _geminiClient = geminiClient;
            _backgroundJobClient = backgroundJobClient;
            _aiSettings = aiSettings.Value;
            _trendSettings = trendSettings.Value;
            _time = time;
            _logger = logger;
        }

        private DateTime UtcNow => _time.GetUtcNow().UtcDateTime;

        // ------------------------------------------------------------------
        // Feed
        // ------------------------------------------------------------------

        public async Task<TrendFeedResponse> GetTrendingTopicsAsync(TrendFeedQuery query)
        {
            var (platform, category, sample) = ValidateAndNormalizeQuery(query);
            var since = UtcNow.AddDays(-Math.Clamp(query.Days, 1, 365));
            var limit = Math.Clamp(query.Limit, 1, 100);

            var candidates = await FilterVideos(platform, category, sample, since, query.ShortsOnly)
                .Include(v => v.Channel)
                .OrderByDescending(v => v.LatestViews)
                .Take(MaxCandidates)
                .ToListAsync();

            var populations = await LoadVelocityPopulationsAsync(since);
            var ranked = RankByVelocity(candidates, limit);
            var items = await EnrichWithSnapshotsAsync(ranked, populations);

            return new TrendFeedResponse
            {
                Items = items,
                TotalMatched = candidates.Count,
                LastCollectedAt = await LastCollectedAtAsync(),
                CollectorConfigured = _youTube.IsConfigured,
                Notice = BuildNotice(platform, items.Count)
            };
        }

        private (string? Platform, string? Category, string Sample) ValidateAndNormalizeQuery(TrendFeedQuery query)
        {
            var platform = NormalizeFilter(query.Platform);
            if (platform != null && !TrendPlatforms.All.Contains(platform))
            {
                throw new BadRequestException($"Nền tảng không hợp lệ. Chọn một trong: all, {string.Join(", ", TrendPlatforms.All)}.");
            }

            var category = NormalizeFilter(query.Category);
            var sample = NormalizeFilter(query.Sample) ?? "trending";
            if (!SampleFilters.Contains(sample))
            {
                throw new BadRequestException($"Nhóm mẫu không hợp lệ. Chọn một trong: {string.Join(", ", SampleFilters)}.");
            }

            return (platform, category, sample);
        }

        private IQueryable<TrendVideo> FilterVideos(
            string? platform, string? category, string sample, DateTime since, bool shortsOnly)
        {
            var query = _context.TrendVideos.AsNoTracking()
                .Where(v => (v.PublishedAt ?? v.FirstSeenAt) >= since);

            if (platform != null) query = query.Where(v => v.Platform == platform);
            if (category != null) query = query.Where(v => v.CategorySlug == category);
            if (shortsOnly) query = query.Where(v => v.IsShort);

            return sample switch
            {
                "popular" => query.Where(v => v.InPopularChart),
                "viral" => query.Where(v => v.InViralSample),
                "control" => query.Where(v => v.InControlSample),
                "manual" => query.Where(v => v.SubmittedByUserId != null || v.DataSource != TrendDataSources.YouTubeApi),
                "all" => query,
                _ => query.Where(v => v.InPopularChart || v.InViralSample
                    || v.SubmittedByUserId != null || v.DataSource != TrendDataSources.YouTubeApi)
            };
        }

        private static List<(TrendVideo Video, double? Velocity)> RankByVelocity(List<TrendVideo> candidates, int limit) =>
            candidates
                .Select(v => (Video: v, Velocity: Velocity(v)))
                .OrderByDescending(x => x.Velocity.HasValue)
                .ThenByDescending(x => x.Velocity)
                .ThenByDescending(x => x.Video.LatestViews)
                .Take(limit)
                .ToList();

        private async Task<List<TrendTopicDto>> EnrichWithSnapshotsAsync(
            List<(TrendVideo Video, double? Velocity)> ranked,
            Dictionary<(string Platform, string Category), List<double>> populations)
        {
            var ids = ranked.Select(x => x.Video.Id).ToList();
            var snapshots = await _context.TrendVideoSnapshots.AsNoTracking()
                .Where(s => ids.Contains(s.TrendVideoId))
                .OrderByDescending(s => s.CapturedAt)
                .ToListAsync();
            var snapshotsByVideo = snapshots.GroupBy(s => s.TrendVideoId).ToDictionary(g => g.Key, g => g.ToList());

            return ranked.Select(x =>
            {
                var history = snapshotsByVideo.GetValueOrDefault(x.Video.Id) ?? new List<TrendVideoSnapshot>();
                var population = populations.GetValueOrDefault((x.Video.Platform, x.Video.CategorySlug)) ?? new List<double>();
                return x.Video.ToTopicDto(
                    previousSnapshot: history.Skip(1).FirstOrDefault(),
                    snapshotCount: history.Count,
                    viralityScore: TrendMetrics.PercentileRank(x.Velocity, population));
            }).ToList();
        }

        /// <summary>
        /// Quần thể views/giờ theo (nền tảng, danh mục) trên toàn bộ dữ liệu có số liệu, kể cả mẫu đối chứng,
        /// để điểm virality là thứ hạng so với quần thể chứ không chỉ so với các video đang hiển thị.
        /// </summary>
        private async Task<Dictionary<(string Platform, string Category), List<double>>> LoadVelocityPopulationsAsync(DateTime since)
        {
            var rows = await _context.TrendVideos.AsNoTracking()
                .Where(v => (v.PublishedAt ?? v.FirstSeenAt) >= since && v.LatestViews != null && v.PublishedAt != null)
                .Select(v => new { v.Platform, v.CategorySlug, v.PublishedAt, v.LastSnapshotAt, v.FirstSeenAt, v.LatestViews })
                .ToListAsync();

            return rows
                .Select(r => new
                {
                    Key = (r.Platform, r.CategorySlug),
                    Velocity = TrendMetrics.ViewsPerHour(r.LatestViews, TrendMetrics.AgeHours(r.PublishedAt, r.LastSnapshotAt ?? r.FirstSeenAt))
                })
                .Where(r => r.Velocity.HasValue)
                .GroupBy(r => r.Key)
                .ToDictionary(g => g.Key, g => g.Select(r => r.Velocity!.Value).ToList());
        }

        private static double? Velocity(TrendVideo video) =>
            TrendMetrics.ViewsPerHour(video.LatestViews, TrendMetrics.AgeHours(video.PublishedAt, video.LastSnapshotAt ?? video.FirstSeenAt));

        private string? BuildNotice(string? platform, int itemCount)
        {
            if (platform is TrendPlatforms.TikTok or TrendPlatforms.Reels)
            {
                return "TikTok và Facebook/Instagram không cung cấp API công khai để đọc video của người khác. "
                    + "Video ở mục này do người dùng thêm bằng link; số liệu được đánh dấu là nhập tay.";
            }

            if (itemCount > 0) return null;

            return _youTube.IsConfigured
                ? "Chưa có dữ liệu phù hợp. Bộ thu thập chạy định kỳ mỗi ngày; quản trị viên có thể kích hoạt thu thập thủ công."
                : "Máy chủ chưa cấu hình YouTube API key nên chưa thu thập được dữ liệu xu hướng.";
        }

        private async Task<DateTime?> LastCollectedAtAsync() =>
            await _context.TrendCollectionRuns.AsNoTracking()
                .Where(r => r.Kind == "discover" && r.Status == TrendRunStatuses.Succeeded)
                .MaxAsync(r => r.FinishedAt);

        private static string? NormalizeFilter(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var normalized = value.Trim().ToLowerInvariant();
            return normalized == "all" ? null : normalized;
        }

        // ------------------------------------------------------------------
        // Nhập tay, lịch sử, thống kê, kích hoạt thu thập
        // ------------------------------------------------------------------

        public async Task<TrendTopicDto> ImportManualAsync(string userId, ManualTrendImportRequest request)
        {
            var video = await _manualImporter.ImportAsync(userId, request);

            var channel = video.TrendChannelId == null ? null : await _context.TrendChannels.FindAsync(video.TrendChannelId);
            video.Channel ??= channel;
            var history = await _context.TrendVideoSnapshots.AsNoTracking()
                .Where(s => s.TrendVideoId == video.Id)
                .OrderByDescending(s => s.CapturedAt)
                .ToListAsync();

            return video.ToTopicDto(history.Skip(1).FirstOrDefault(), history.Count, viralityScore: null);
        }

        public async Task<IReadOnlyList<TrendSnapshotDto>> GetSnapshotsAsync(string trendVideoId)
        {
            var exists = await _context.TrendVideos.AnyAsync(v => v.Id == trendVideoId);
            if (!exists) throw new NotFoundException($"Không tìm thấy video xu hướng với ID {trendVideoId}");

            var snapshots = await _context.TrendVideoSnapshots.AsNoTracking()
                .Where(s => s.TrendVideoId == trendVideoId)
                .OrderBy(s => s.CapturedAt)
                .ToListAsync();

            return snapshots.Select(s => s.ToDto()).ToList();
        }

        public async Task<TrendCollectionStatsDto> GetCollectionStatsAsync()
        {
            var videos = _context.TrendVideos.AsNoTracking();
            var quota = await _collector.GetQuotaStatusAsync();

            var multiSnapshot = await _context.TrendVideoSnapshots
                .GroupBy(s => s.TrendVideoId)
                .Where(g => g.Count() >= 2)
                .CountAsync();

            var milestoneRows = await _context.TrendVideoSnapshots
                .Where(s => s.MilestoneHours != null)
                .GroupBy(s => s.MilestoneHours!.Value)
                .Select(g => new { Milestone = g.Key, Count = g.Count() })
                .ToListAsync();

            var recentRuns = await _context.TrendCollectionRuns.AsNoTracking()
                .OrderByDescending(r => r.StartedAt)
                .Take(20)
                .ToListAsync();

            return new TrendCollectionStatsDto
            {
                CollectorConfigured = _youTube.IsConfigured,
                CollectorEnabled = _trendSettings.Enabled,
                TotalVideos = await videos.CountAsync(),
                TrackingVideos = await videos.CountAsync(v => v.IsTracking),
                TotalSnapshots = await _context.TrendVideoSnapshots.CountAsync(),
                VideosWithMultipleSnapshots = multiSnapshot,
                TotalChannels = await _context.TrendChannels.CountAsync(),
                VideosByPlatform = await CountByAsync(videos, v => v.Platform),
                VideosByCategory = await CountByAsync(videos, v => v.CategorySlug),
                VideosByDataSource = await CountByAsync(videos, v => v.DataSource),
                VideosBySample = new Dictionary<string, int>
                {
                    [TrendVideoMapper.SampleNames.Popular] = await videos.CountAsync(v => v.InPopularChart),
                    [TrendVideoMapper.SampleNames.Viral] = await videos.CountAsync(v => v.InViralSample),
                    [TrendVideoMapper.SampleNames.Control] = await videos.CountAsync(v => v.InControlSample),
                    [TrendVideoMapper.SampleNames.Manual] = await videos.CountAsync(v => v.SubmittedByUserId != null || v.DataSource != TrendDataSources.YouTubeApi)
                },
                SnapshotsByMilestone = milestoneRows
                    .OrderBy(r => r.Milestone)
                    .ToDictionary(r => r.Milestone.ToString(CultureInfo.InvariantCulture) + "h", r => r.Count),
                QuotaUsedToday = quota.UsedToday,
                QuotaDailyBudget = quota.DailyBudget,
                ProviderQuotaExceeded = quota.ProviderQuotaExceeded,
                LastCollectedAt = await LastCollectedAtAsync(),
                RecentRuns = recentRuns.Select(r => r.ToDto()).ToList()
            };
        }

        public TrendCollectionEnqueuedDto EnqueueDiscover(string? category)
        {
            var slug = NormalizeFilter(category);
            if (slug != null && !_trendSettings.CategoryMap.ContainsKey(slug))
            {
                throw new BadRequestException($"Danh mục không hợp lệ. Chọn một trong: all, {string.Join(", ", _trendSettings.CategoryMap.Keys)}.");
            }
            EnsureCollectorReady();

            var jobId = _backgroundJobClient.Enqueue<ITrendCollector>(c => c.DiscoverAsync(slug));
            _logger.LogInformation("Đã đưa job thu thập xu hướng vào hàng đợi: {JobId} ({Category}).", jobId, slug ?? "all");
            return new TrendCollectionEnqueuedDto { JobId = jobId, Kind = "discover", Category = slug };
        }

        public TrendCollectionEnqueuedDto EnqueueSnapshot()
        {
            EnsureCollectorReady();
            var jobId = _backgroundJobClient.Enqueue<ITrendCollector>(c => c.SnapshotDueAsync());
            return new TrendCollectionEnqueuedDto { JobId = jobId, Kind = "snapshot" };
        }

        private void EnsureCollectorReady()
        {
            if (!_trendSettings.Enabled) throw new BadRequestException("Bộ thu thập xu hướng đang tắt (TrendCollection:Enabled).");
            if (!_youTube.IsConfigured) throw new BadRequestException("Máy chủ chưa cấu hình YouTube API key.");
        }

        private static async Task<Dictionary<string, int>> CountByAsync(
            IQueryable<TrendVideo> videos, System.Linq.Expressions.Expression<Func<TrendVideo, string>> key)
        {
            var rows = await videos.GroupBy(key).Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
            return rows.ToDictionary(r => r.Key, r => r.Count);
        }

        // ------------------------------------------------------------------
        // Phân tích bằng AI (ý kiến của mô hình, không phải số liệu đo được)
        // ------------------------------------------------------------------

        public async Task<TrendAnalysisResponse> AnalyzeTrendAsync(string userId, AnalyzeTrendRequest request)
        {
            var measuredContext = await BuildMeasuredContextAsync(request.TrendVideoId);

            var openRouterKey = !string.IsNullOrWhiteSpace(_aiSettings?.OpenRouter?.ApiKey)
                ? _aiSettings.OpenRouter.ApiKey
                : Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");

            if (!string.IsNullOrWhiteSpace(openRouterKey))
            {
                try
                {
                    var result = await CallOpenRouterForAnalysisAsync(openRouterKey, request, measuredContext);
                    if (result != null)
                    {
                        await LogApiUsageAsync(userId, "OpenRouter", 1, 0.003m);
                        return result;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "OpenRouter Trend Analysis lỗi: {Msg}", ex.Message);
                }
            }

            try
            {
                var promptText = BuildTrendAnalysisPrompt(request, measuredContext);
                var options = new GeminiRequestOptions(
                    SystemInstruction: "Bạn là chuyên gia phân tích kịch bản video viral và giải mã cấu trúc giữ chân người xem. Luôn trả về đúng schema JSON thuần túy.",
                    Temperature: 0.7);

                var geminiResult = await _geminiClient.GenerateAsync(promptText, options);
                var cleanJson = ExtractJsonObject(geminiResult.Text);
                var parsed = JsonSerializer.Deserialize<TrendAnalysisResponse>(cleanJson, JsonOptions);

                if (parsed != null)
                {
                    parsed.Topic = request.Topic;
                    parsed.Platform = request.Platform;
                    parsed.ModelUsed = geminiResult.Model;
                    parsed.ProviderUsed = "Google Gemini";
                    await LogApiUsageAsync(userId, "Gemini", 1, 0.005m);
                    return parsed;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Gemini Trend Analysis lỗi: {Msg}", ex.Message);
            }

            throw new BadRequestException("Không thể phân tích video xu hướng lúc này do dịch vụ AI tạm thời gián đoạn. Vui lòng thử lại sau.");
        }

        /// <summary>Số liệu đo được của video (nếu có) để đưa vào prompt, tránh để AI tự suy diễn con số.</summary>
        private async Task<string?> BuildMeasuredContextAsync(string? trendVideoId)
        {
            if (string.IsNullOrWhiteSpace(trendVideoId)) return null;

            var video = await _context.TrendVideos.AsNoTracking()
                .Include(v => v.Channel)
                .FirstOrDefaultAsync(v => v.Id == trendVideoId);
            if (video == null) return null;

            var age = TrendMetrics.AgeHours(video.PublishedAt, video.LastSnapshotAt ?? video.FirstSeenAt);
            var lines = new List<string> { $"Nguồn số liệu: {video.DataSource}, đo lúc {video.LastSnapshotAt:yyyy-MM-dd HH:mm} UTC." };
            if (video.LatestViews.HasValue) lines.Add($"Lượt xem: {video.LatestViews:N0}");
            if (video.LatestLikes.HasValue) lines.Add($"Lượt thích: {video.LatestLikes:N0}");
            if (video.LatestComments.HasValue) lines.Add($"Bình luận: {video.LatestComments:N0}");
            if (video.Channel?.SubscriberCount is { } subs) lines.Add($"Subscriber của kênh: {subs:N0}");
            if (age.HasValue) lines.Add($"Tuổi video lúc đo: {age.Value:0} giờ");
            if (video.DurationSeconds.HasValue) lines.Add($"Thời lượng: {video.DurationSeconds} giây");

            return string.Join("\n", lines);
        }

        private async Task<TrendAnalysisResponse?> CallOpenRouterForAnalysisAsync(string apiKey, AnalyzeTrendRequest request, string? measuredContext)
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(60);

            var url = !string.IsNullOrWhiteSpace(_aiSettings?.OpenRouter?.BaseUrl)
                ? _aiSettings.OpenRouter.BaseUrl
                : "https://openrouter.ai/api/v1/chat/completions";

            var promptText = BuildTrendAnalysisPrompt(request, measuredContext);

            var requestBody = new
            {
                model = "openrouter/free",
                messages = new object[]
                {
                    new { role = "system", content = "Bạn là chuyên gia phân tích kịch bản video viral và giải mã cấu trúc giữ chân người xem. Luôn trả về đúng schema JSON thuần túy." },
                    new { role = "user", content = promptText }
                },
                temperature = 0.7
            };

            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, url);
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            requestMessage.Headers.Add("HTTP-Referer", "https://postflow.io");
            requestMessage.Headers.Add("X-Title", "PostFlow Video Analysis");
            requestMessage.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            using var response = await client.SendAsync(requestMessage);
            if (!response.IsSuccessStatusCode) return null;

            var responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);

            if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0) return null;

            var contentText = choices[0].GetProperty("message").GetProperty("content").GetString();
            if (string.IsNullOrWhiteSpace(contentText)) return null;

            var cleanJson = ExtractJsonObject(contentText);
            var parsed = JsonSerializer.Deserialize<TrendAnalysisResponse>(cleanJson, JsonOptions);

            if (parsed != null)
            {
                parsed.Topic = request.Topic;
                parsed.Platform = request.Platform;
                parsed.ModelUsed = "openrouter/free";
                parsed.ProviderUsed = "OpenRouter AI";
            }

            return parsed;
        }

        internal static string BuildTrendAnalysisPrompt(AnalyzeTrendRequest request, string? measuredContext)
        {
            var measured = string.IsNullOrWhiteSpace(measuredContext)
                ? "Không có số liệu đo được. Không được tự đưa ra con số lượt xem/lượt thích."
                : measuredContext + "\nChỉ dùng đúng các con số trên, không tự thêm số liệu khác.";

            return $@"Phân tích kịch bản và cấu trúc của video sau:
                Tiêu đề video: {request.Topic}
                Nền tảng: {request.Platform}
                Danh mục: {request.Category ?? "Chung"}
                Hashtags: {(request.Hashtags != null ? string.Join(", ", request.Hashtags) : "Không có")}
                Ngữ cảnh / Mô tả: {request.AdditionalContext ?? "Không có"}

                SỐ LIỆU ĐO ĐƯỢC:
                {measured}

                Hãy giải mã vì sao video này thu hút người xem, tâm lý người xem, cấu trúc kịch bản và đưa ra 3 góc làm lại video tương tự nhưng sáng tạo và cuốn hút hơn.

                BẮT BUỘC TRẢ VỀ JSON THUẦN TÚY:
                {{
                ""viralityScore"": 96,
                ""whyTrendingReason"": ""Giải thích chi tiết 2-3 câu vì sao video này thu hút (Hook 3s, nhịp độ dựng, tâm lý tò mò/FOMO)..."",
                ""targetAudience"": ""Mô tả tệp khán giả mục tiêu (độ tuổi, sở thích, hành vi)"",
                ""emotionalTriggers"": [
                    ""Tò mò & Bất ngờ"",
                    ""Khát khao phát triển"",
                    ""Tiết kiệm thời gian""
                ],
                ""angles"": [
                    {{
                    ""angleTitle"": ""Góc 1: Kể lại trải nghiệm thực tế (Storytelling)"",
                    ""targetHook"": ""Câu hook mở đầu 3s giữ chân người xem"",
                    ""contentBrief"": ""Quy trình 3 bước triển khai nội dung trong 45s""
                    }},
                    {{
                    ""angleTitle"": ""Góc 2: Phản biện / Lật ngược góc nhìn thông thường"",
                    ""targetHook"": ""Câu hook thách thức quan điểm cũ"",
                    ""contentBrief"": ""Đưa ra bằng chứng và giải pháp đột phá""
                    }},
                    {{
                    ""angleTitle"": ""Góc 3: Hướng dẫn thực hành nhanh (Tutorial)"",
                    ""targetHook"": ""Câu hook hành động tạo kết quả tức thì"",
                    ""contentBrief"": ""Các bước thực hiện nhanh gọn dễ làm theo""
                    }}
                ],
                ""recommendedHashtags"": [""#Hashtag1"", ""#Hashtag2"", ""#Hashtag3"", ""#Hashtag4""],
                ""suggestedCta"": ""Lời kêu gọi hành động cuối video tăng follow và comment"",
                ""generatedVideoIdea"": ""Đoạn prompt hoàn chỉnh để sinh video mới tương tự""
            }}";
        }

        private static string ExtractJsonObject(string rawText)
        {
            if (string.IsNullOrWhiteSpace(rawText)) return string.Empty;
            var text = rawText.Trim();
            var first = text.IndexOf('{');
            var last = text.LastIndexOf('}');
            if (first >= 0 && last > first)
            {
                return text.Substring(first, last - first + 1);
            }
            return text;
        }

        private async Task LogApiUsageAsync(string userId, string provider, int count, decimal cost)
        {
            var today = DateTime.UtcNow.Date;
            var log = await _context.ApiUsageLogs
                .FirstOrDefaultAsync(l => l.UserId == userId && l.Provider == provider && l.Date == today);

            if (log == null)
            {
                log = new ApiUsageLog
                {
                    UserId = userId,
                    Provider = provider,
                    CallCount = count,
                    CostEstimate = cost,
                    Date = today
                };
                _context.ApiUsageLogs.Add(log);
            }
            else
            {
                log.CallCount += count;
                log.CostEstimate += cost;
            }

            await _context.SaveChangesAsync();
        }
    }
}
