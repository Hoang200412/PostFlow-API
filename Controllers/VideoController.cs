using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PostFlow.Data;
using PostFlow.Models;
using PostFlow.Services;
using PostFlow.Services.Interfaces;
using Hangfire;

namespace PostFlow.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/videos")]
    public class VideoController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IStorageService _storageService;
        private readonly IBackgroundJobClient _backgroundJobClient;

        public VideoController(
            AppDbContext context,
            IStorageService storageService,
            IBackgroundJobClient backgroundJobClient)
        {
            _context = context;
            _storageService = storageService;
            _backgroundJobClient = backgroundJobClient;
        }

        private string? GetCurrentUserId() =>
            User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        [HttpGet]
        public async Task<IActionResult> GetList([FromQuery] int page = 1, [FromQuery] int limit = 10)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var query = _context.VideoAssets
                .Where(v => v.UserId == userId)
                .OrderByDescending(v => v.CreatedAt);

            var total = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * limit)
                .Take(limit)
                .ToListAsync();

            return Ok(new
            {
                items,
                total,
                page,
                limit
            });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var video = await _context.VideoAssets
                .FirstOrDefaultAsync(v => v.Id == id && v.UserId == userId);

            if (video == null) return NotFound();

            return Ok(video);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var video = await _context.VideoAssets
                .FirstOrDefaultAsync(v => v.Id == id && v.UserId == userId);

            if (video == null) return NotFound();

            var hasActiveJobs = await _context.UploadJobs
                .AnyAsync(j => j.VideoAssetId == id && (j.Status == UploadStatus.Pending || j.Status == UploadStatus.Processing));

            if (hasActiveJobs || video.Status == "Rendering")
            {
                return Conflict(new { message = "Không thể xóa video khi đang render hoặc đang tải lên." });
            }

            if (!string.IsNullOrEmpty(video.LocalFilePath))
            {
                await _storageService.DeleteFileAsync(video.LocalFilePath);
            }

            _context.VideoAssets.Remove(video);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpPost("generate")]
        public async Task<IActionResult> Generate([FromBody] VideoGenerateRequest request)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            if (string.IsNullOrWhiteSpace(request.Topic))
            {
                return BadRequest(new { message = "Topic cannot be empty." });
            }

            var dna = await _context.ContentDNAs
                .FirstOrDefaultAsync(d => d.Id == request.ContentDnaId && d.UserId == userId);
            if (dna == null)
            {
                return BadRequest(new { message = "Content DNA không tồn tại hoặc không thuộc quyền sở hữu." });
            }

            if (request.ChannelIds != null && request.ChannelIds.Length > 0)
            {
                var channelsCount = await _context.SocialAccounts
                    .CountAsync(sa => request.ChannelIds.Contains(sa.Id) && sa.UserId == userId);
                if (channelsCount != request.ChannelIds.Length)
                {
                    return BadRequest(new { message = "Một số kênh truyền hình liên kết không tồn tại." });
                }
            }

            var today = DateTime.Today;
            var currentQuota = await _context.ApiUsageLogs
                .Where(l => l.UserId == userId && l.Date == today)
                .SumAsync(l => l.CallCount);

            if (currentQuota >= 100)
            {
                return StatusCode(403, new { message = "Hạn mức cuộc gọi API (Quota) trong ngày đã vượt ngưỡng." });
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

            return Accepted(new { videoId = video.Id, status = video.Status });
        }
    }

    public class VideoGenerateRequest
    {
        public string Topic { get; set; } = string.Empty;
        public string ContentDnaId { get; set; } = string.Empty;
        public string[]? ChannelIds { get; set; }
        public DateTime? ScheduledAt { get; set; }
    }
}
