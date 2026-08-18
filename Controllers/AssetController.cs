using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PostFlow.Data;
using PostFlow.Models;
using PostFlow.Services.Interfaces;

namespace PostFlow.Controllers
{
    [ApiController]
    [Route("api/assets")]
    public class AssetController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IStorageService _storageService;
        private readonly IVideoUploadService _videoUploadService;

        public AssetController(
            AppDbContext context,
            IStorageService storageService,
            IVideoUploadService videoUploadService)
        {
            _context = context;
            _storageService = storageService;
            _videoUploadService = videoUploadService;
        }

        private string? GetCurrentUserId() =>
            User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        [Authorize]
        [HttpPost("presigned-upload")]
        public async Task<IActionResult> GetPresignedUploadUrl([FromBody] PresignedUploadRequest request)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            if (string.IsNullOrWhiteSpace(request.Filename))
            {
                return BadRequest(new { message = "Filename cannot be empty." });
            }

            if (!request.ContentType.StartsWith("video/"))
            {
                return BadRequest(new { message = "Chỉ hỗ trợ định dạng video." });
            }

            if (request.FileSize > 500 * 1024 * 1024) // 500MB
            {
                return StatusCode(413, new { message = "File quá lớn, dung lượng tối đa là 500MB." });
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

            var requestScheme = Request.Scheme;
            var requestHost = Request.Host;
            var uploadUrl = $"{requestScheme}://{requestHost}/api/assets/upload-binary/{video.Id}";

            return Ok(new
            {
                uploadUrl,
                videoId = video.Id
            });
        }

        [HttpPut("upload-binary/{videoId}")]
        public async Task<IActionResult> UploadBinary(string videoId)
        {
            var video = await _context.VideoAssets.FindAsync(videoId);
            if (video == null) return NotFound();

            var ext = ".mp4";
            var fileName = $"{videoId}{ext}";

            using var ms = new MemoryStream();
            await Request.Body.CopyToAsync(ms);
            ms.Position = 0;

            if (ms.Length == 0)
            {
                return BadRequest(new { message = "Không tìm thấy nội dung file upload." });
            }

            var relativePath = await _storageService.SaveFileAsync(ms, $"uploaded-videos/{video.UserId}", fileName);
            
            video.LocalFilePath = relativePath;
            video.DurationSeconds = 30;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Upload successful", localPath = relativePath });
        }

        [Authorize]
        [HttpPost("upload-confirm")]
        public async Task<IActionResult> UploadConfirm([FromBody] UploadConfirmRequest request)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var video = await _context.VideoAssets
                .FirstOrDefaultAsync(v => v.Id == request.VideoId && v.UserId == userId);

            if (video == null) return NotFound(new { message = "Không tìm thấy thông tin video." });

            if (string.IsNullOrEmpty(video.LocalFilePath) || !await _storageService.FileExistsAsync(video.LocalFilePath))
            {
                return NotFound(new { message = "File upload không tìm thấy trên hệ thống lưu trữ, vui lòng upload lại." });
            }

            video.Status = "Ready";
            await _context.SaveChangesAsync();

            var postingJobs = new System.Collections.Generic.List<UploadJob>();

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

            return Created($"/api/videos/{video.Id}", new
            {
                videoId = video.Id,
                postingJobs = postingJobs.Select(j => new { j.Id, j.Platform, j.Status, j.ScheduledAt })
            });
        }
    }

    public class PresignedUploadRequest
    {
        public string Filename { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string ContentType { get; set; } = "video/mp4";
    }

    public class UploadConfirmRequest
    {
        public string VideoId { get; set; } = string.Empty;
        public string[] ChannelIds { get; set; } = new string[0];
        public DateTime? ScheduledAt { get; set; }
    }
}
