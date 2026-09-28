using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PostFlow.Exceptions;
using PostFlow.Models;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Models.DTOs.Responses;
using PostFlow.Services.Interfaces;

namespace PostFlow.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/publishing-jobs")]
    public class PublishingJobController : ControllerBase
    {
        private readonly IPublishingService _publishingService;

        public PublishingJobController(IPublishingService publishingService)
        {
            _publishingService = publishingService;
        }

        private string? GetCurrentUserId() =>
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        /// <summary>
        /// API Lấy danh sách tác vụ xuất bản / đăng bài có hỗ trợ bộ lọc
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetList([FromQuery] UploadStatus? status, [FromQuery] string? channelId)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var list = await _publishingService.GetListAsync(userId, status, channelId);
            return Ok(list);
        }

        /// <summary>
        /// API Lấy chi tiết tác vụ xuất bản theo ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var job = await _publishingService.GetByIdAsync(id);
            if (job == null)
            {
                throw new NotFoundException($"Không tìm thấy Publishing Job với ID {id}");
            }
            return Ok(job);
        }

        /// <summary>
        /// API Đẩy một tác vụ xuất bản mới vào hàng đợi Hangfire (Đăng ngay hoặc Đặt lịch)
        /// </summary>
        [HttpPost("enqueue")]
        public async Task<IActionResult> Enqueue([FromBody] EnqueueUploadRequest request)
        {
            var userId = GetCurrentUserId();
            var targetUserId = !string.IsNullOrEmpty(userId) ? userId : request.UserId;

            var job = await _publishingService.EnqueueJobAsync(
                targetUserId,
                request.VideoAssetId,
                request.SocialAccountId,
                request.ScheduledAt,
                request.PlatformMetadataOverride);

            return AcceptedAtAction(nameof(GetById), new { id = job.Id }, job.ToDto());
        }

        /// <summary>
        /// API Cập nhật lịch đăng bài hoặc ghi đè metadata
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] PostingJobUpdateRequest request)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var job = await _publishingService.UpdateAsync(userId, id, request);
            return Ok(job.ToDto());
        }

        /// <summary>
        /// API Hủy tác vụ xuất bản đang chờ xử lý
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Cancel(string id)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            await _publishingService.CancelAsync(userId, id);
            return NoContent();
        }
    }
}
