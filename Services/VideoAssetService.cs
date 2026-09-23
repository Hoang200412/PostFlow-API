using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PostFlow.Configuration;
using PostFlow.Data;
using PostFlow.Exceptions;
using PostFlow.Models;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Models.DTOs.Responses;
using PostFlow.Services.Interfaces;

namespace PostFlow.Services
{
    public class VideoAssetService : IVideoAssetService
    {
        private readonly AppDbContext _context;
        private readonly IStorageService _storageService;
        private readonly IPublishingService _publishingService;
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly CloudflareR2Settings _r2Settings;

        public VideoAssetService(
            AppDbContext context,
            IStorageService storageService,
            IPublishingService publishingService,
            IBackgroundJobClient backgroundJobClient,
            IOptions<CloudflareR2Settings> r2Settings)
        {
            _context = context;
            _storageService = storageService;
            _publishingService = publishingService;
            _backgroundJobClient = backgroundJobClient;
            _r2Settings = r2Settings.Value;
        }

        public async Task<PagedResult<VideoAssetDto>> GetListAsync(string userId, int page, int limit)
        {
            page = page <= 0 ? 1 : page;
            limit = limit <= 0 ? 10 : limit;

            var query = _context.VideoAssets
                .AsNoTracking()
                .Where(v => v.UserId == userId)
                .OrderByDescending(v => v.CreatedAt);

            var total = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * limit)
                .Take(limit)
                .ToListAsync();

            return new PagedResult<VideoAssetDto>(
                items.Select(v => v.ToDto()),
                total,
                page,
                limit
            );
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
            if (file.Length <= 0)
            {
                throw new BadRequestException("File upload không có dữ liệu.");
            }

            if (file.Length > _r2Settings.MaxFileSizeBytes)
            {
                throw new PayloadTooLargeException($"Dung lượng file vượt quá giới hạn cho phép ({_r2Settings.MaxFileSizeMB}MB).");
            }

            var video = new VideoAsset
            {
                UserId = userId,
                Title = !string.IsNullOrWhiteSpace(title) ? title : Path.GetFileNameWithoutExtension(file.FileName),
                Description = description ?? string.Empty,
                Tags = tags ?? new List<string>(),
                Status = "Ready",
                CreatedAt = DateTime.UtcNow
            };

            var ext = Path.GetExtension(file.FileName);
            if (string.IsNullOrEmpty(ext)) ext = ".mp4";
            var now = DateTime.UtcNow;
            var folderName = $"videos/{userId}/{now:yyyy}/{now:MM}";
            var fileName = $"{video.Id}{ext}";

            using var stream = file.OpenReadStream();
            var storagePath = await _storageService.SaveFileAsync(stream, folderName, fileName, file.ContentType);

            video.LocalFilePath = storagePath;
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

        public async Task<object> GetPresignedUploadUrlAsync(string userId, PresignedUploadRequest request, string scheme, string host)
        {
            if (string.IsNullOrWhiteSpace(request.Filename))
            {
                throw new BadRequestException("Filename cannot be empty.");
            }

            if (!request.ContentType.StartsWith("video/"))
            {
                throw new BadRequestException("Chỉ hỗ trợ định dạng video.");
            }

            if (request.FileSize > _r2Settings.MaxFileSizeBytes)
            {
                throw new PayloadTooLargeException($"File quá lớn, dung lượng tối đa là {_r2Settings.MaxFileSizeMB}MB.");
            }

            var video = new VideoAsset
            {
                UserId = userId,
                Title = Path.GetFileNameWithoutExtension(request.Filename),
                Description = "Video tải lên thủ công",
                Status = "Uploading",
                CreatedAt = DateTime.UtcNow
            };

            var ext = Path.GetExtension(request.Filename);
            if (string.IsNullOrEmpty(ext)) ext = ".mp4";
            var now = DateTime.UtcNow;
            var folderName = $"videos/{userId}/{now:yyyy}/{now:MM}";
            var fileName = $"{video.Id}{ext}";
            var objectKey = $"{folderName}/{fileName}";

            video.LocalFilePath = objectKey;
            _context.VideoAssets.Add(video);
            await _context.SaveChangesAsync();

            var uploadUrl = await _storageService.GeneratePresignedUploadUrlAsync(folderName, fileName, _r2Settings.PresignedUrlExpiryMinutes, request.ContentType);

            return new
            {
                uploadUrl,
                videoId = video.Id,
                objectKey
            };
        }

        public async Task<object> UploadBinaryAsync(string videoId, Stream bodyStream)
        {
            var video = await _context.VideoAssets.FindAsync(videoId)
                ?? throw new NotFoundException($"Không tìm thấy video với ID {videoId}");

            var ext = ".mp4";
            var now = DateTime.UtcNow;
            var folderName = $"videos/{video.UserId}/{now:yyyy}/{now:MM}";
            var fileName = $"{videoId}{ext}";

            using var ms = new MemoryStream();
            await bodyStream.CopyToAsync(ms);
            ms.Position = 0;

            if (ms.Length == 0)
            {
                throw new BadRequestException("Không tìm thấy nội dung file upload.");
            }

            if (ms.Length > _r2Settings.MaxFileSizeBytes)
            {
                throw new PayloadTooLargeException($"Dung lượng file vượt quá giới hạn cho phép ({_r2Settings.MaxFileSizeMB}MB).");
            }

            var storagePath = await _storageService.SaveFileAsync(ms, folderName, fileName, "video/mp4");

            video.LocalFilePath = storagePath;
            video.DurationSeconds = 30;
            await _context.SaveChangesAsync();

            return new { message = "Upload successful", localPath = storagePath };
        }

        public async Task<object> UploadConfirmAsync(string userId, UploadConfirmRequest request)
        {
            var video = await _context.VideoAssets
                .FirstOrDefaultAsync(v => v.Id == request.VideoId && v.UserId == userId)
                ?? throw new NotFoundException("Không tìm thấy thông tin video.");

            if (string.IsNullOrEmpty(video.LocalFilePath) || !await _storageService.FileExistsAsync(video.LocalFilePath))
            {
                throw new NotFoundException("File upload không tìm thấy trên hệ thống lưu trữ Cloudflare R2, vui lòng upload lại.");
            }

            var actualSize = await _storageService.GetFileSizeAsync(video.LocalFilePath);
            if (actualSize <= 0)
            {
                throw new BadRequestException("File upload trên Cloudflare R2 rỗng hoặc quá trình upload chưa hoàn tất.");
            }

            if (actualSize > _r2Settings.MaxFileSizeBytes)
            {
                await _storageService.DeleteFileAsync(video.LocalFilePath);
                throw new PayloadTooLargeException($"Dung lượng file thực tế ({actualSize / (1024 * 1024)}MB) vượt quá giới hạn cho phép ({_r2Settings.MaxFileSizeMB}MB).");
            }

            video.Status = "Ready";
            await _context.SaveChangesAsync();

            var postingJobs = new List<UploadJob>();

            if (request.ChannelIds != null && request.ChannelIds.Length > 0)
            {
                foreach (var channelId in request.ChannelIds)
                {
                    try
                    {
                        var job = await _publishingService.EnqueueJobAsync(userId, video.Id, channelId, request.ScheduledAt, null);
                        postingJobs.Add(job);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Confirm upload job error: {ex.Message}");
                    }
                }
            }

            return new
            {
                videoId = video.Id,
                postingJobs = postingJobs.Select(j => new { j.Id, j.Platform, j.Status, j.ScheduledAt })
            };
        }
    }
}
