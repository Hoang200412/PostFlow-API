using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PostFlow.Data;
using PostFlow.Models;
using PostFlow.Services.Interfaces;
using Hangfire;

namespace PostFlow.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/posting-jobs")]
    public class PostingJobController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IBackgroundJobClient _backgroundJobClient;

        public PostingJobController(AppDbContext context, IBackgroundJobClient backgroundJobClient)
        {
            _context = context;
            _backgroundJobClient = backgroundJobClient;
        }

        private string? GetCurrentUserId() =>
            User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        [HttpGet]
        public async Task<IActionResult> GetList([FromQuery] UploadStatus? status, [FromQuery] string? channelId)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var query = _context.UploadJobs
                .Where(j => j.UserId == userId);

            if (status.HasValue)
            {
                query = query.Where(j => j.Status == status.Value);
            }

            if (!string.IsNullOrEmpty(channelId))
            {
                query = query.Where(j => j.SocialAccountId == channelId);
            }

            var list = await query
                .Include(j => j.VideoAsset)
                .Include(j => j.SocialAccount)
                .OrderByDescending(j => j.CreatedAt)
                .ToListAsync();

            return Ok(list.Select(j => new {
                j.Id,
                j.Platform,
                j.Status,
                j.ProgressPercentage,
                j.ErrorMessage,
                j.VideoUrl,
                j.ScheduledAt,
                j.PlatformMetadataOverride,
                j.PlatformPostId,
                j.RetryCount,
                j.NextRetryAt,
                j.CreatedAt,
                j.CompletedAt,
                Video = j.VideoAsset == null ? null : new { j.VideoAsset.Id, j.VideoAsset.Title, j.VideoAsset.Status },
                Channel = j.SocialAccount == null ? null : new { j.SocialAccount.Id, j.SocialAccount.AccountName, j.SocialAccount.Platform }
            }));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] PostingJobUpdateRequest request)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var job = await _context.UploadJobs
                .Include(j => j.VideoAsset)
                .FirstOrDefaultAsync(j => j.Id == id && j.UserId == userId);

            if (job == null) return NotFound();

            if (job.Status != UploadStatus.Pending)
            {
                return Conflict(new { message = "Không thể chỉnh sửa job đã hoàn thành hoặc đang xử lý." });
            }

            if (request.ScheduledAt.HasValue)
            {
                if (request.ScheduledAt.Value < DateTime.UtcNow.AddMinutes(5))
                {
                    return BadRequest(new { message = "Thời gian đặt lịch phải sau ít nhất 5 phút." });
                }
                job.ScheduledAt = request.ScheduledAt.Value;

                var delay = job.ScheduledAt.Value - DateTime.UtcNow;
                _backgroundJobClient.Schedule<IVideoUploadService>(s => s.ProcessUploadJobAsync(job.Id, System.Threading.CancellationToken.None), delay);
            }

            if (request.PlatformMetadataOverride != null)
            {
                job.PlatformMetadataOverride = request.PlatformMetadataOverride;
            }

            await _context.SaveChangesAsync();
            return Ok(job);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Cancel(string id)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var job = await _context.UploadJobs
                .FirstOrDefaultAsync(j => j.Id == id && j.UserId == userId);

            if (job == null) return NotFound();

            if (job.Status != UploadStatus.Pending)
            {
                return Conflict(new { message = "Không thể hủy job đã hoàn thành hoặc đang xử lý." });
            }

            job.Status = UploadStatus.Cancelled;
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }

    public class PostingJobUpdateRequest
    {
        public DateTime? ScheduledAt { get; set; }
        public string? PlatformMetadataOverride { get; set; }
    }
}
