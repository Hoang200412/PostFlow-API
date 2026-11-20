using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PostFlow.Configuration;
using PostFlow.Data;
using PostFlow.Exceptions;
using PostFlow.Models;
using PostFlow.Models.DTOs.Responses;
using PostFlow.Services.Interfaces;

namespace PostFlow.Services.Trends
{
    public class TrendCollector : ITrendCollector
    {
        private static readonly TimeSpan MinSnapshotInterval = TimeSpan.FromHours(1);
        private static readonly TimeSpan ChannelRefreshInterval = TimeSpan.FromDays(3);
        private const int MaxDescriptionLength = 5000;

        private readonly AppDbContext _context;
        private readonly IYouTubeDataClient _youTube;
        private readonly TrendCollectionSettings _settings;
        private readonly TimeProvider _time;
        private readonly ILogger<TrendCollector> _logger;

        public TrendCollector(
            AppDbContext context,
            IYouTubeDataClient youTube,
            IOptions<TrendCollectionSettings> settings,
            TimeProvider time,
            ILogger<TrendCollector> logger)
        {
            _context = context;
            _youTube = youTube;
            _settings = settings.Value;
            _time = time;
            _logger = logger;
        }

        private DateTime UtcNow => _time.GetUtcNow().UtcDateTime;

        public async Task<TrendQuotaStatus> GetQuotaStatusAsync()
        {
            var dayStart = YouTubeQuotaDay.StartUtc(UtcNow);
            var used = await _context.TrendCollectionRuns
                .Where(r => r.StartedAt >= dayStart)
                .SumAsync(r => r.QuotaUnitsUsed);
            var exceeded = await _context.TrendCollectionRuns
                .AnyAsync(r => r.StartedAt >= dayStart && r.Status == TrendRunStatuses.QuotaExceeded);

            return new TrendQuotaStatus(used, _settings.DailyQuotaBudget, exceeded, dayStart);
        }

        public async Task<TrendCollectionSummary> DiscoverAsync(string? categorySlug)
        {
            var skip = await CheckPrerequisitesAsync();
            if (skip != null) return skip;

            var ledger = await CreateLedgerAsync();
            var categories = ResolveCategories(categorySlug);
            var runs = new List<TrendCollectionRun>();
            var touchedChannels = new HashSet<string>();
            var now = UtcNow;

            foreach (var (slug, youTubeCategoryId) in categories)
            {
                if (ledger.Stopped) break;
                await CollectPopularAsync(slug, youTubeCategoryId, ledger, runs, touchedChannels);

                if (ledger.Stopped) break;
                await CollectViralAsync(slug, youTubeCategoryId, ledger, runs, touchedChannels, now);

                if (ledger.Stopped) break;
                await CollectControlAsync(slug, youTubeCategoryId, ledger, runs, touchedChannels, now);
            }

            if (!ledger.Stopped && touchedChannels.Count > 0)
            {
                await CollectChannelsAsync(touchedChannels, ledger, runs);
            }

            return await SummarizeAsync(runs, ledger);
        }

        public async Task<TrendCollectionSummary> SnapshotDueAsync()
        {
            var skip = await CheckPrerequisitesAsync();
            if (skip != null) return skip;

            var ledger = await CreateLedgerAsync();
            var now = UtcNow;
            var due = await FindDueVideosAsync(now);

            if (due.Count == 0)
            {
                return await SummarizeAsync(Array.Empty<TrendCollectionRun>(), ledger);
            }

            var run = await ExecuteRunAsync("snapshot", "snapshot", null, ledger, async r =>
            {
                await ProcessSnapshotBatchesAsync(due, ledger, r, now);
            });

            return await SummarizeAsync(new[] { run }, ledger);
        }

        public async Task<TrendVideo> ImportYouTubeVideoAsync(string videoId, string categorySlug, string? userId, CancellationToken cancellationToken = default)
        {
            if (!_youTube.IsConfigured)
            {
                throw new BadRequestException("Máy chủ chưa cấu hình YouTube API key nên chưa thể thêm video YouTube.");
            }

            var skip = await CheckPrerequisitesAsync();
            if (skip != null) throw new BadRequestException(skip.Message ?? "Đã hết quota YouTube hôm nay.");

            var ledger = await CreateLedgerAsync();
            TrendVideo? imported = null;

            var run = await ExecuteRunAsync("import", "manual", categorySlug, ledger, async r =>
            {
                ledger.Charge(YouTubeQuotaCost.List);
                var items = await _youTube.GetVideosAsync(new[] { videoId }, cancellationToken);
                if (items.Count == 0) return;

                var touched = new HashSet<string>();
                var videos = await UpsertVideosAsync(items, categorySlug, v => v.SubmittedByUserId ??= userId, r, touched);
                imported = videos.FirstOrDefault();

                if (touched.Count > 0 && !ledger.Stopped)
                {
                    await RefreshChannelsAsync(touched, ledger, r);
                }
            });

            if (imported != null) return imported;
            if (run.Status == TrendRunStatuses.Succeeded)
                throw new NotFoundException("Không tìm thấy video YouTube này (có thể đã bị xóa hoặc ở chế độ riêng tư).");

            throw new BadRequestException($"Không lấy được dữ liệu từ YouTube: {run.ErrorMessage ?? run.Status}");
        }

        // ------------------------------------------------------------------
        // Thu thập theo từng luồng
        // ------------------------------------------------------------------

        private async Task CollectPopularAsync(
            string slug, string? categoryId, QuotaLedger ledger, List<TrendCollectionRun> runs, HashSet<string> touchedChannels)
        {
            var run = await ExecuteRunAsync("discover", "popular", slug, ledger, async r =>
            {
                ledger.Charge(YouTubeQuotaCost.List);
                var items = await _youTube.GetMostPopularAsync(_settings.RegionCode, categoryId, _settings.PopularMaxResults);
                await UpsertVideosAsync(items, slug, v => v.InPopularChart = true, r, touchedChannels);
            });
            runs.Add(run);
        }

        private async Task CollectViralAsync(
            string slug, string? categoryId, QuotaLedger ledger, List<TrendCollectionRun> runs, HashSet<string> touchedChannels, DateTime now)
        {
            var run = await ExecuteRunAsync("discover", "viral", slug, ledger, async r =>
            {
                var query = new YouTubeSearchQuery(
                    _settings.RegionCode, _settings.RelevanceLanguage, categoryId,
                    PublishedAfter: now.AddDays(-_settings.LookbackDays), PublishedBefore: null, Order: "viewCount");
                var items = await SearchAndFetchAsync(query, ledger);
                await UpsertVideosAsync(items, slug, v => v.InViralSample = true, r, touchedChannels);
            });
            runs.Add(run);
        }

        private async Task CollectControlAsync(
            string slug, string? categoryId, QuotaLedger ledger, List<TrendCollectionRun> runs, HashSet<string> touchedChannels, DateTime now)
        {
            var run = await ExecuteRunAsync("discover", "control", slug, ledger, async r =>
            {
                var (after, before) = ControlSampleWindow.For(now, _settings.LookbackDays);
                var query = new YouTubeSearchQuery(
                    _settings.RegionCode, _settings.RelevanceLanguage, categoryId,
                    PublishedAfter: after, PublishedBefore: before, Order: "date");
                var items = await SearchAndFetchAsync(query, ledger);
                await UpsertVideosAsync(items, slug, v => v.InControlSample = true, r, touchedChannels);
            });
            runs.Add(run);
        }

        private async Task CollectChannelsAsync(
            HashSet<string> touchedChannels, QuotaLedger ledger, List<TrendCollectionRun> runs)
        {
            var run = await ExecuteRunAsync("discover", "channels", null, ledger,
                r => RefreshChannelsAsync(touchedChannels, ledger, r));
            runs.Add(run);
        }

        // ------------------------------------------------------------------
        // Xử lý Snapshot Due
        // ------------------------------------------------------------------

        private async Task<List<(TrendVideo Video, int Milestone)>> FindDueVideosAsync(DateTime now)
        {
            var tracked = await _context.TrendVideos
                .Where(v => v.IsTracking
                    && v.Platform == TrendPlatforms.YouTube
                    && v.DataSource == TrendDataSources.YouTubeApi
                    && v.PublishedAt != null)
                .ToListAsync();

            var recorded = await LoadRecordedMilestonesAsync(tracked.Select(v => v.Id).ToList());
            var due = new List<(TrendVideo Video, int Milestone)>();
            var untracked = 0;

            foreach (var video in tracked)
            {
                var age = TrendMetrics.AgeHours(video.PublishedAt, now)!.Value;
                if (SnapshotPlanner.ShouldStopTracking(age, _settings.Milestones, _settings.TrackingDays))
                {
                    video.IsTracking = false;
                    untracked++;
                    continue;
                }

                var milestone = SnapshotPlanner.DueMilestone(age, recorded.GetValueOrDefault(video.Id) ?? new HashSet<int>(), _settings.Milestones);
                if (milestone.HasValue) due.Add((video, milestone.Value));
            }

            if (untracked > 0)
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Ngừng theo dõi {Count} video đã quá tuổi theo dõi.", untracked);
            }

            return due;
        }

        private async Task ProcessSnapshotBatchesAsync(
            List<(TrendVideo Video, int Milestone)> due, QuotaLedger ledger, TrendCollectionRun run, DateTime now)
        {
            foreach (var batch in due.Chunk(YouTubeQuotaCost.MaxIdsPerList))
            {
                ledger.Charge(YouTubeQuotaCost.List);
                var items = await _youTube.GetVideosAsync(batch.Select(d => d.Video.ExternalId).ToList());
                var byId = items.ToDictionary(i => i.Id);

                foreach (var (video, milestone) in batch)
                {
                    run.VideosFound++;
                    if (!byId.TryGetValue(video.ExternalId, out var item))
                    {
                        video.IsTracking = false;
                        continue;
                    }

                    AddSnapshot(video, item.Views, item.Likes, item.Comments, now, milestone, TrendDataSources.YouTubeApi);
                    run.SnapshotsWritten++;
                }

                await _context.SaveChangesAsync();
            }
        }

        // ------------------------------------------------------------------
        // Quota & Tiền điều kiện
        // ------------------------------------------------------------------

        private async Task<TrendCollectionSummary?> CheckPrerequisitesAsync()
        {
            if (!_settings.Enabled)
                return Skipped("Bộ thu thập đang tắt (TrendCollection:Enabled = false).");

            if (!_youTube.IsConfigured)
            {
                _logger.LogWarning("Bỏ qua thu thập xu hướng: chưa cấu hình YouTube API key.");
                return Skipped("Chưa cấu hình YouTube API key.");
            }

            var quota = await GetQuotaStatusAsync();
            if (quota.ProviderQuotaExceeded)
                return Skipped("YouTube đã báo hết quota trong ngày; chờ quota reset lúc 0h giờ Pacific.", quota.UsedToday);

            if (quota.UsedToday >= quota.DailyBudget)
                return Skipped($"Đã dùng hết ngân sách quota tự đặt ({quota.UsedToday}/{quota.DailyBudget}).", quota.UsedToday);

            return null;
        }

        private async Task<QuotaLedger> CreateLedgerAsync()
        {
            var quota = await GetQuotaStatusAsync();
            return new QuotaLedger(quota.UsedToday, quota.DailyBudget);
        }

        private IReadOnlyList<(string Slug, string? YouTubeCategoryId)> ResolveCategories(string? categorySlug)
        {
            if (string.IsNullOrWhiteSpace(categorySlug) || categorySlug.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                return _settings.CategoryMap.Select(c => (c.Key, (string?)c.Value)).ToList();
            }

            var slug = categorySlug.Trim().ToLowerInvariant();
            if (!_settings.CategoryMap.TryGetValue(slug, out var id))
            {
                throw new BadRequestException($"Danh mục '{categorySlug}' không nằm trong cấu hình thu thập ({string.Join(", ", _settings.CategoryMap.Keys)}).");
            }
            return new[] { (slug, (string?)id) };
        }

        private async Task<TrendCollectionRun> ExecuteRunAsync(
            string kind, string source, string? categorySlug, QuotaLedger ledger, Func<TrendCollectionRun, Task> body)
        {
            var run = new TrendCollectionRun
            {
                Kind = kind,
                Source = source,
                CategorySlug = categorySlug,
                Platform = TrendPlatforms.YouTube,
                StartedAt = UtcNow
            };
            var usedBefore = ledger.UsedThisRun;

            try
            {
                await body(run);
                run.Status = TrendRunStatuses.Succeeded;
            }
            catch (QuotaBudgetExhaustedException ex)
            {
                run.Status = TrendRunStatuses.BudgetExhausted;
                run.ErrorMessage = ex.Message;
            }
            catch (YouTubeApiException ex) when (ex.IsQuotaExceeded)
            {
                ledger.MarkProviderExhausted();
                run.Status = TrendRunStatuses.QuotaExceeded;
                run.ErrorMessage = ex.Message;
                _logger.LogWarning("YouTube báo hết quota trong lượt {Kind}/{Source}; dừng thu thập tới khi quota reset.", kind, source);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                run.Status = TrendRunStatuses.Failed;
                run.ErrorMessage = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
                _logger.LogError(ex, "Lượt thu thập {Kind}/{Source} ({Category}) thất bại.", kind, source, categorySlug ?? "-");
            }

            run.QuotaUnitsUsed = ledger.UsedThisRun - usedBefore;
            run.FinishedAt = UtcNow;
            _context.TrendCollectionRuns.Add(run);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Không lưu được dữ liệu của lượt {Kind}/{Source}; chỉ ghi nhật ký lượt chạy.", kind, source);
                _context.ChangeTracker.Clear();
                run.Status = TrendRunStatuses.Failed;
                run.ErrorMessage ??= "Lỗi lưu dữ liệu: " + ex.GetBaseException().Message;
                _context.TrendCollectionRuns.Add(run);
                await _context.SaveChangesAsync();
            }

            return run;
        }

        private async Task<IReadOnlyList<YouTubeVideoItem>> SearchAndFetchAsync(YouTubeSearchQuery query, QuotaLedger ledger)
        {
            var ids = new List<string>();
            string? pageToken = null;
            var pages = Math.Max(1, _settings.SearchPagesPerQuery);

            for (var page = 0; page < pages; page++)
            {
                ledger.Charge(YouTubeQuotaCost.Search);
                var result = await _youTube.SearchShortVideoIdsAsync(query with { PageToken = pageToken });
                ids.AddRange(result.VideoIds.Where(id => !ids.Contains(id)).ToList());
                pageToken = result.NextPageToken;
                if (string.IsNullOrEmpty(pageToken)) break;
            }

            var items = new List<YouTubeVideoItem>();
            foreach (var batch in ids.Chunk(YouTubeQuotaCost.MaxIdsPerList))
            {
                ledger.Charge(YouTubeQuotaCost.List);
                items.AddRange(await _youTube.GetVideosAsync(batch));
            }
            return items;
        }

        private async Task<List<TrendVideo>> UpsertVideosAsync(
            IReadOnlyList<YouTubeVideoItem> items,
            string categorySlug,
            Action<TrendVideo> markSample,
            TrendCollectionRun run,
            ISet<string> touchedChannels)
        {
            var distinct = items.GroupBy(i => i.Id).Select(g => g.First()).ToList();
            if (distinct.Count == 0) return new List<TrendVideo>();

            var now = UtcNow;
            var ids = distinct.Select(i => i.Id).ToList();
            var existing = await _context.TrendVideos
                .Where(v => v.Platform == TrendPlatforms.YouTube && ids.Contains(v.ExternalId))
                .ToDictionaryAsync(v => v.ExternalId);
            var recorded = await LoadRecordedMilestonesAsync(existing.Values.Select(v => v.Id).ToList());

            var result = new List<TrendVideo>();
            foreach (var item in distinct)
            {
                var video = GetOrCreateVideo(item, categorySlug, existing, now, run);
                ApplyMetadata(video, item);
                markSample(video);
                run.VideosFound++;

                if (!string.IsNullOrWhiteSpace(item.ChannelId)) touchedChannels.Add(item.ChannelId);

                TryRecordSnapshot(video, item, recorded, now, run);
                result.Add(video);
            }

            await _context.SaveChangesAsync();
            return result;
        }

        private TrendVideo GetOrCreateVideo(
            YouTubeVideoItem item, string categorySlug, Dictionary<string, TrendVideo> existing, DateTime now, TrendCollectionRun run)
        {
            if (existing.TryGetValue(item.Id, out var video)) return video;

            video = new TrendVideo
            {
                Platform = TrendPlatforms.YouTube,
                ExternalId = item.Id,
                CategorySlug = categorySlug,
                DataSource = TrendDataSources.YouTubeApi,
                FirstSeenAt = now
            };
            _context.TrendVideos.Add(video);
            existing[item.Id] = video;
            run.VideosNew++;

            var ageAtDiscovery = TrendMetrics.AgeHours(item.PublishedAt, now);
            video.IsTracking = ageAtDiscovery.HasValue
                && !SnapshotPlanner.ShouldStopTracking(ageAtDiscovery.Value, _settings.Milestones, _settings.TrackingDays);

            return video;
        }

        private void TryRecordSnapshot(
            TrendVideo video, YouTubeVideoItem item, Dictionary<string, HashSet<int>> recorded, DateTime now, TrendCollectionRun run)
        {
            if (video.LastSnapshotAt != null && now - video.LastSnapshotAt.Value < MinSnapshotInterval) return;

            var age = TrendMetrics.AgeHours(video.PublishedAt, now);
            var milestone = age.HasValue
                ? SnapshotPlanner.DueMilestone(age.Value, recorded.GetValueOrDefault(video.Id) ?? new HashSet<int>(), _settings.Milestones)
                : null;

            AddSnapshot(video, item.Views, item.Likes, item.Comments, now, milestone, TrendDataSources.YouTubeApi);
            run.SnapshotsWritten++;
        }

        private void ApplyMetadata(TrendVideo video, YouTubeVideoItem item)
        {
            video.Title = item.Title;
            video.Description = item.Description.Length > MaxDescriptionLength ? item.Description[..MaxDescriptionLength] : item.Description;
            video.Tags = item.Tags.ToList();
            video.ThumbnailUrl = item.ThumbnailUrl;
            video.ChannelExternalId = item.ChannelId;
            video.ChannelTitle = item.ChannelTitle;
            video.PublishedAt = item.PublishedAt;
            video.DurationSeconds = item.DurationSeconds;
            video.IsShort = ShortsClassifier.IsShort(item.DurationSeconds, item.Title, item.Description, item.Tags, _settings.ShortsMaxSeconds);
            video.PlatformCategoryId = item.CategoryId;
            video.Language = item.Language;
            video.Url = video.IsShort ? $"https://www.youtube.com/shorts/{item.Id}" : $"https://www.youtube.com/watch?v={item.Id}";
            video.EmbedUrl = $"https://www.youtube.com/embed/{item.Id}";
        }

        private void AddSnapshot(TrendVideo video, long? views, long? likes, long? comments, DateTime capturedAt, int? milestone, string dataSource) =>
            _context.TrendVideoSnapshots.Add(TrendSnapshots.Record(video, views, likes, comments, capturedAt, milestone, dataSource));

        private async Task RefreshChannelsAsync(IEnumerable<string> channelIds, QuotaLedger ledger, TrendCollectionRun run)
        {
            var now = UtcNow;
            var ids = channelIds.Distinct().ToList();
            var existing = await _context.TrendChannels
                .Where(c => c.Platform == TrendPlatforms.YouTube && ids.Contains(c.ExternalId))
                .ToDictionaryAsync(c => c.ExternalId);

            var stale = ids.Where(id => !existing.TryGetValue(id, out var c) || now - c.UpdatedAt >= ChannelRefreshInterval).ToList();

            foreach (var batch in stale.Chunk(YouTubeQuotaCost.MaxIdsPerList))
            {
                ledger.Charge(YouTubeQuotaCost.List);
                foreach (var item in await _youTube.GetChannelsAsync(batch))
                {
                    if (!existing.TryGetValue(item.Id, out var channel))
                    {
                        channel = new TrendChannel { Platform = TrendPlatforms.YouTube, ExternalId = item.Id };
                        _context.TrendChannels.Add(channel);
                        existing[item.Id] = channel;
                    }

                    channel.Title = item.Title;
                    channel.AvatarUrl = item.AvatarUrl;
                    channel.SubscriberCount = item.SubscriberCount;
                    channel.VideoCount = item.VideoCount;
                    channel.UpdatedAt = now;
                }
            }

            var unlinked = await _context.TrendVideos
                .Where(v => v.Platform == TrendPlatforms.YouTube && v.TrendChannelId == null && ids.Contains(v.ChannelExternalId))
                .ToListAsync();
            foreach (var video in unlinked)
            {
                if (existing.TryGetValue(video.ChannelExternalId, out var channel)) video.TrendChannelId = channel.Id;
            }

            await _context.SaveChangesAsync();
        }

        private async Task<Dictionary<string, HashSet<int>>> LoadRecordedMilestonesAsync(List<string> videoIds)
        {
            if (videoIds.Count == 0) return new Dictionary<string, HashSet<int>>();

            var rows = await _context.TrendVideoSnapshots
                .Where(s => videoIds.Contains(s.TrendVideoId) && s.MilestoneHours != null)
                .Select(s => new { s.TrendVideoId, Milestone = s.MilestoneHours!.Value })
                .ToListAsync();

            return rows.GroupBy(r => r.TrendVideoId).ToDictionary(g => g.Key, g => g.Select(r => r.Milestone).ToHashSet());
        }

        private async Task<TrendCollectionSummary> SummarizeAsync(IReadOnlyList<TrendCollectionRun> runs, QuotaLedger ledger)
        {
            var quota = await GetQuotaStatusAsync();
            var status = runs.Count == 0
                ? TrendRunStatuses.Succeeded
                : runs.FirstOrDefault(r => r.Status == TrendRunStatuses.QuotaExceeded)?.Status
                    ?? runs.FirstOrDefault(r => r.Status == TrendRunStatuses.BudgetExhausted)?.Status
                    ?? (runs.All(r => r.Status == TrendRunStatuses.Failed) ? TrendRunStatuses.Failed : TrendRunStatuses.Succeeded);

            var failed = runs.Count(r => r.Status == TrendRunStatuses.Failed);
            var message = runs.Count == 0
                ? "Không có video nào đến hạn."
                : failed > 0 ? $"{failed}/{runs.Count} lượt thất bại; xem TrendCollectionRuns để biết chi tiết." : null;

            var summary = new TrendCollectionSummary(
                status,
                runs.Sum(r => r.VideosFound),
                runs.Sum(r => r.VideosNew),
                runs.Sum(r => r.SnapshotsWritten),
                ledger.UsedThisRun,
                quota.UsedToday,
                message);

            _logger.LogInformation(
                "Thu thập xu hướng xong: {Status}, {Found} video ({New} mới), {Snapshots} snapshot, {Quota} đơn vị quota (hôm nay {Today}/{Budget}).",
                summary.Status, summary.VideosFound, summary.VideosNew, summary.SnapshotsWritten, summary.QuotaUnitsUsed, quota.UsedToday, quota.DailyBudget);

            return summary;
        }

        private static TrendCollectionSummary Skipped(string message, int usedToday = 0) =>
            new(TrendRunStatuses.Skipped, 0, 0, 0, 0, usedToday, message);

        private sealed class QuotaBudgetExhaustedException : Exception
        {
            public QuotaBudgetExhaustedException(string message) : base(message) { }
        }

        private sealed class QuotaLedger
        {
            private readonly int _usedBefore;
            private readonly int _budget;

            public QuotaLedger(int usedBefore, int budget)
            {
                _usedBefore = usedBefore;
                _budget = budget;
            }

            public int UsedThisRun { get; private set; }
            public bool ProviderExhausted { get; private set; }
            public bool BudgetReached { get; private set; }
            public bool Stopped => ProviderExhausted || BudgetReached;

            public void Charge(int units)
            {
                if (ProviderExhausted) throw new QuotaBudgetExhaustedException("YouTube đã báo hết quota.");
                if (_usedBefore + UsedThisRun + units > _budget)
                {
                    BudgetReached = true;
                    throw new QuotaBudgetExhaustedException($"Chạm ngân sách quota ngày ({_usedBefore + UsedThisRun}/{_budget}).");
                }
                UsedThisRun += units;
            }

            public void MarkProviderExhausted() => ProviderExhausted = true;
        }
    }
}
