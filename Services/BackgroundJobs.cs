using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PostFlow.Data;
using PostFlow.Models;
using PostFlow.Services.Interfaces;

namespace PostFlow.Services
{
    public interface IBackgroundJobs
    {
        Task RenderVideoJobAsync(
            string videoId,
            string topic,
            string? contentDnaId,
            string scriptJson,
            string? voice,
            string[] channelIds,
            string? scheduledAtIsoStr);
        Task GatherAnalyticsJobAsync();
    }

    public class BackgroundJobs : IBackgroundJobs
    {
        private readonly AppDbContext _context;
        private readonly IVideoGenerationService _videoGenService;
        private readonly IPublishingService _publishingService;
        private readonly ILogger<BackgroundJobs> _logger;

        public BackgroundJobs(
            AppDbContext context,
            IVideoGenerationService videoGenService,
            IPublishingService publishingService,
            ILogger<BackgroundJobs> logger)
        {
            _context = context;
            _videoGenService = videoGenService;
            _publishingService = publishingService;
            _logger = logger;
        }

        public async Task RenderVideoJobAsync(
            string videoId,
            string topic,
            string? contentDnaId,
            string scriptJson,
            string? voice,
            string[] channelIds,
            string? scheduledAtIsoStr)
        {
            var video = await _context.VideoAssets.FindAsync(videoId);
            if (video == null)
            {
                _logger.LogError("Không tìm thấy VideoAsset để render. VideoId: {VideoId}", videoId);
                return;
            }

            try
            {
                _logger.LogInformation("Bắt đầu sinh video tự động bằng AI. VideoId: {VideoId}, Chủ đề: {Topic}", videoId, topic);
                video.Status = "Rendering";
                await _context.SaveChangesAsync();

                var relativePath = await _videoGenService.GenerateVideoAsync(
                    video.UserId,
                    topic,
                    contentDnaId,
                    scriptJson,
                    voice);

                video.LocalFilePath = relativePath;
                video.Status = "Ready";
                video.DurationSeconds = 15; // Độ dài video mặc định giả lập
                await _context.SaveChangesAsync();

                _logger.LogInformation("Sinh video thành công! File: {Path}. Tiến hành tạo Posting Jobs cho các kênh...", relativePath);

                DateTime? scheduledAt = null;
                if (!string.IsNullOrEmpty(scheduledAtIsoStr))
                {
                    if (DateTime.TryParse(scheduledAtIsoStr, out var parsedDate))
                    {
                        scheduledAt = parsedDate.ToUniversalTime();
                    }
                }

                foreach (var channelId in channelIds)
                {
                    try
                    {
                        await _publishingService.EnqueueJobAsync(video.UserId, video.Id, channelId, scheduledAt, null);
                        _logger.LogInformation("Đã tạo PublishingJob cho Kênh {ChannelId}", channelId);
                    }
                    catch (Exception jobEx)
                    {
                        _logger.LogError(jobEx, "Lỗi tạo PublishingJob cho Kênh {ChannelId}", channelId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi trong tiến trình RenderVideoJobAsync cho Video {VideoId}", videoId);
                video.Status = "Failed";
                await _context.SaveChangesAsync();
            }
        }

        public async Task GatherAnalyticsJobAsync()
        {
            _logger.LogInformation("Bắt đầu thu thập Analytics định kỳ...");

            // Lấy tất cả các jobs đã đăng thành công (Completed)
            var postedJobs = await _context.UploadJobs
                .Where(j => j.Status == UploadStatus.Completed)
                .ToListAsync();

            if (!postedJobs.Any())
            {
                _logger.LogInformation("Không có video đã đăng nào để thu thập số liệu.");
                return;
            }

            var random = new Random();
            int updatedCount = 0;

            foreach (var job in postedJobs)
            {
                // Giả lập tăng tiến số liệu views, likes, shares, comments, saves
                int currentViews = 0;
                int currentLikes = 0;
                int currentShares = 0;
                int currentComments = 0;
                int currentSaves = 0;

                var lastSnapshot = await _context.Analytics
                    .Where(a => a.UploadJobId == job.Id)
                    .OrderByDescending(a => a.CapturedAt)
                    .FirstOrDefaultAsync();

                if (lastSnapshot != null)
                {
                    currentViews = lastSnapshot.Views + random.Next(100, 1000);
                    currentLikes = lastSnapshot.Likes + random.Next(10, 100);
                    currentShares = lastSnapshot.Shares + random.Next(2, 20);
                    currentComments = lastSnapshot.Comments + random.Next(1, 10);
                    currentSaves = lastSnapshot.Saves + random.Next(1, 15);
                }
                else
                {
                    currentViews = random.Next(500, 2000);
                    currentLikes = (int)(currentViews * 0.1) + random.Next(10, 50);
                    currentShares = (int)(currentLikes * 0.15) + random.Next(1, 10);
                    currentComments = (int)(currentLikes * 0.05) + random.Next(1, 5);
                    currentSaves = (int)(currentLikes * 0.08) + random.Next(1, 8);
                }

                var snapshot = new Analytics
                {
                    UploadJobId = job.Id,
                    Views = currentViews,
                    Likes = currentLikes,
                    Shares = currentShares,
                    Comments = currentComments,
                    Saves = currentSaves,
                    CapturedAt = DateTime.UtcNow
                };

                _context.Analytics.Add(snapshot);
                job.LastAnalyticsAt = DateTime.UtcNow;
                updatedCount++;
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Đã cập nhật Analytics snapshot cho {Count} jobs thành công.", updatedCount);
        }
    }
}
