using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PostFlow.Models;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Services.Interfaces;

namespace PostFlow.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/posting-jobs")]
    public class PostingJobController : ControllerBase
    {
        private readonly IPostingJobService _postingJobService;

        public PostingJobController(IPostingJobService postingJobService)
        {
            _postingJobService = postingJobService;
        }

        private string? GetCurrentUserId() =>
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        /// <summary>
        /// API Lấy danh sách nhiệm vụ đăng bài (Posting Jobs) của người dùng
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetList([FromQuery] UploadStatus? status, [FromQuery] string? channelId)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var list = await _postingJobService.GetListAsync(userId, status, channelId);
            return Ok(list);
        }

        /// <summary>
        /// API Cập nhật thông tin nhiệm vụ đăng bài (Lên lịch lại hoặc ghi đè metadata)
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] PostingJobUpdateRequest request)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var job = await _postingJobService.UpdateAsync(userId, id, request);
            return Ok(job);
        }

        /// <summary>
        /// API Hủy nhiệm vụ đăng bài đang chờ xử lý
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Cancel(string id)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            await _postingJobService.CancelAsync(userId, id);
            return NoContent();
        }
    }
}
