using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PostFlow.Data;
using PostFlow.Exceptions;
using PostFlow.Models;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Models.DTOs.Responses;
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
                items = items.Select(v => v.ToDto()),
                total,
                page,
                limit
            };
        }

        public async Task<VideoAssetDto> GetByIdAsync(string userId, string id)
        {
            var video = await _context.VideoAssets
                .FirstOrDefaultAsync(v => v.Id == id && v.UserId == userId)
                ?? throw new NotFoundException($"Không tìm thấy video với ID {id}");

            return video.ToDto();
        }

        public async Task<VideoAssetDto> UpdateAsync(string userId, string id, VideoUpdateRequest request)
        {
            var video = await _context.VideoAssets
                .FirstOrDefaultAsync(v => v.Id == id && v.UserId == userId)
                ?? throw new NotFoundException($"Không tìm thấy video với ID {id}");

            if (request.Title != null) video.Title = request.Title;
            if (request.Description != null) video.Description = request.Description;
            if (request.Tags != null) video.Tags = request.Tags;
            video.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return video.ToDto();
        }

        public async Task<VideoAssetDto> UploadVideoAsync(string userId, IFormFile file, string? title, string? description, List<string>? tags)
        {
            var tempDir = Path.Combine(Directory.GetCurrentDirectory(), "temp_uploads");
            if (!Directory.Exists(tempDir))
            {
                Directory.CreateDirectory(tempDir);
            }

            var tempFileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var tempFilePath = Path.Combine(tempDir, tempFileName);

            using (var stream = new FileStream(tempFilePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var video = new VideoAsset
            {
                UserId = userId,
                Title = !string.IsNullOrWhiteSpace(title) ? title : Path.GetFileNameWithoutExtension(file.FileName),
                Description = description ?? string.Empty,
                Tags = tags ?? new List<string>(),
                LocalFilePath = tempFilePath,
                Status = "Ready",
                CreatedAt = DateTime.UtcNow
            };

            _context.VideoAssets.Add(video);
            await _context.SaveChangesAsync();

            return video.ToDto();
        }

        public async Task DeleteAsync(string userId, string id)
        {
            var video = await _context.VideoAssets
                .FirstOrDefaultAsync(v => v.Id == id && v.UserId == userId)
                ?? throw new NotFoundException($"Không tìm thấy video với ID {id}");

            if (!string.IsNullOrEmpty(video.LocalFilePath))
            {
                try { await _storageService.DeleteFileAsync(video.LocalFilePath); } catch { }
            }

            _context.VideoAssets.Remove(video);
            await _context.SaveChangesAsync();
        }

        public async Task BatchDeleteAsync(string userId, List<string> ids)
        {
            var videos = await _context.VideoAssets
                .Where(v => ids.Contains(v.Id) && v.UserId == userId)
                .ToListAsync();

            foreach (var video in videos)
            {
                if (!string.IsNullOrEmpty(video.LocalFilePath))
                {
                    try { await _storageService.DeleteFileAsync(video.LocalFilePath); } catch { }
                }
                _context.VideoAssets.Remove(video);
            }

            await _context.SaveChangesAsync();
        }

        public async Task<object> GenerateAsync(string userId, VideoGenerateRequest request)
        {
            var dna = await _context.ContentDNAs
                .FirstOrDefaultAsync(d => d.Id == request.ContentDnaId && d.UserId == userId)
                ?? throw new BadRequestException("Content DNA không tồn tại hoặc không thuộc quyền sở hữu.");

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
