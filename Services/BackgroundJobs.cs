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
            _logger.LogWarning("Analytics collector chưa được cấu hình; bỏ qua lần thu thập này để tránh ghi dữ liệu giả.");
            await Task.CompletedTask;
        }
    }
}
