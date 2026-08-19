using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PostFlow.Data;
using PostFlow.Exceptions;
using PostFlow.Models;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Services.Interfaces;

namespace PostFlow.Services
{
    public class AssetService : IAssetService
    {
        private readonly AppDbContext _context;
        private readonly IStorageService _storageService;
        private readonly IVideoUploadService _videoUploadService;

        public AssetService(
            AppDbContext context,
            IStorageService storageService,
            IVideoUploadService videoUploadService)
        {
            _context = context;
            _storageService = storageService;
            _videoUploadService = videoUploadService;
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

            if (request.FileSize > 500 * 1024 * 1024) // 500MB
            {
                throw new PayloadTooLargeException("File quá lớn, dung lượng tối đa là 500MB.");
            }

            var video = new VideoAsset
            {
                UserId = userId,
                Title = Path.GetFileNameWithoutExtension(request.Filename),
                Description = "Video tải lên thủ công",
                Status = "Uploading",
                LocalFilePath = string.Empty
            };

            _context.VideoAssets.Add(video);
            await _context.SaveChangesAsync();

            var uploadUrl = $"{scheme}://{host}/api/assets/upload-binary/{video.Id}";

            return new
            {
                uploadUrl,
                videoId = video.Id
            };
        }

        public async Task<object> UploadBinaryAsync(string videoId, Stream bodyStream)
        {
            var video = await _context.VideoAssets.FindAsync(videoId);
            if (video == null)
            {
                throw new NotFoundException($"Không tìm thấy video với ID {videoId}");
            }

            var ext = ".mp4";
            var fileName = $"{videoId}{ext}";

            using var ms = new MemoryStream();
            await bodyStream.CopyToAsync(ms);
            ms.Position = 0;

            if (ms.Length == 0)
            {
                throw new BadRequestException("Không tìm thấy nội dung file upload.");
            }

            var relativePath = await _storageService.SaveFileAsync(ms, $"uploaded-videos/{video.UserId}", fileName);

            video.LocalFilePath = relativePath;
            video.DurationSeconds = 30;
            await _context.SaveChangesAsync();

            return new { message = "Upload successful", localPath = relativePath };
        }

        public async Task<object> UploadConfirmAsync(string userId, UploadConfirmRequest request)
        {
            var video = await _context.VideoAssets
                .FirstOrDefaultAsync(v => v.Id == request.VideoId && v.UserId == userId);

            if (video == null)
            {
                throw new NotFoundException("Không tìm thấy thông tin video.");
            }

            if (string.IsNullOrEmpty(video.LocalFilePath) || !await _storageService.FileExistsAsync(video.LocalFilePath))
            {
                throw new NotFoundException("File upload không tìm thấy trên hệ thống lưu trữ, vui lòng upload lại.");
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
                        var job = await _videoUploadService.EnqueueUploadJobAsync(userId, video.Id, channelId, request.ScheduledAt, null);
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
