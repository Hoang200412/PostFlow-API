using System;
using System.Linq;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using PostFlow.Data;
using PostFlow.Exceptions;
using PostFlow.Models;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Services.Interfaces;

namespace PostFlow.Services
{
    public class VideoService : IVideoService
    {
        private readonly AppDbContext _context;
        private readonly IStorageService _storageService;
        private readonly IBackgroundJobClient _backgroundJobClient;

        public VideoService(
            AppDbContext context,
            IStorageService storageService,
            IBackgroundJobClient backgroundJobClient)
        {
            _context = context;
            _storageService = storageService;
            _backgroundJobClient = backgroundJobClient;
        }

        public async Task<object> GetListAsync(string userId, int page, int limit)
        {
            var query = _context.VideoAssets
                .Where(v => v.UserId == userId)
                .OrderByDescending(v => v.CreatedAt);

            var total = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * limit)
                .Take(limit)
                .ToListAsync();

            return new
            {
                items,
                total,
                page,
                limit
            };
        }

        public async Task<VideoAsset> GetByIdAsync(string userId, string id)
        {
            var video = await _context.VideoAssets
                .FirstOrDefaultAsync(v => v.Id == id && v.UserId == userId);

            if (video == null)
            {
                throw new NotFoundException($"Không tìm thấy video với ID {id}");
            }

            return video;
        }

        public async Task DeleteAsync(string userId, string id)
        {
            var video = await _context.VideoAssets
                .FirstOrDefaultAsync(v => v.Id == id && v.UserId == userId);

            if (video == null)
            {
                throw new NotFoundException($"Không tìm thấy video với ID {id}");
            }

            var hasActiveJobs = await _context.UploadJobs
                .AnyAsync(j => j.VideoAssetId == id && (j.Status == UploadStatus.Pending || j.Status == UploadStatus.Processing));

            if (hasActiveJobs || video.Status == "Rendering")
            {
                throw new ConflictException("Không thể xóa video khi đang render hoặc đang tải lên.");
            }

            if (!string.IsNullOrEmpty(video.LocalFilePath))
            {
                await _storageService.DeleteFileAsync(video.LocalFilePath);
            }

            _context.VideoAssets.Remove(video);
            await _context.SaveChangesAsync();
        }

        public async Task<object> GenerateAsync(string userId, VideoGenerateRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Topic))
            {
                throw new BadRequestException("Topic cannot be empty.");
            }

            var dna = await _context.ContentDNAs
                .FirstOrDefaultAsync(d => d.Id == request.ContentDnaId && d.UserId == userId);
            if (dna == null)
            {
                throw new BadRequestException("Content DNA không tồn tại hoặc không thuộc quyền sở hữu.");
            }

            if (request.ChannelIds != null && request.ChannelIds.Length > 0)
            {
                var channelsCount = await _context.SocialAccounts
                    .CountAsync(sa => request.ChannelIds.Contains(sa.Id) && sa.UserId == userId);
                if (channelsCount != request.ChannelIds.Length)
                {
                    throw new BadRequestException("Một số kênh truyền hình liên kết không tồn tại.");
                }
            }

            var today = DateTime.Today;
            var currentQuota = await _context.ApiUsageLogs
                .Where(l => l.UserId == userId && l.Date == today)
                .SumAsync(l => l.CallCount);

            if (currentQuota >= 100)
            {
                throw new ForbiddenException("Hạn mức cuộc gọi API (Quota) trong ngày đã vượt ngưỡng.");
            }

            var video = new VideoAsset
            {
                UserId = userId,
                ContentDNAId = request.ContentDnaId,
                Title = request.Topic,
                Description = $"Video sinh tự động bởi AI với chủ đề: {request.Topic}",
                Status = "Pending",
                LocalFilePath = string.Empty
            };

            _context.VideoAssets.Add(video);
            await _context.SaveChangesAsync();

            var scheduledAtIsoStr = request.ScheduledAt?.ToString("o");
            _backgroundJobClient.Enqueue<IBackgroundJobs>(j =>
                j.RenderVideoJobAsync(video.Id, request.Topic, request.ContentDnaId, request.ChannelIds ?? new string[0], scheduledAtIsoStr));

            return new { videoId = video.Id, status = video.Status };
        }
    }
}
