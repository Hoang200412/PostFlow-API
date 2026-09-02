using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Services.Interfaces;

namespace PostFlow.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/videos")]
    public class VideoController : ControllerBase
    {
        private readonly IVideoService _videoService;

        public VideoController(IVideoService videoService)
        {
            _videoService = videoService;
        }

        private string? GetCurrentUserId() =>
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        /// <summary>
        /// API Lấy danh sách video tài sản của người dùng (Có phân trang)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetList([FromQuery] int page = 1, [FromQuery] int limit = 10)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var result = await _videoService.GetListAsync(userId, page, limit);
            return Ok(result);
        }

        /// <summary>
        /// API Lấy thông tin chi tiết một video theo ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var video = await _videoService.GetByIdAsync(userId, id);
            return Ok(video);
        }

        /// <summary>
        /// API Xóa video tài sản theo ID (Xóa file vật lý và dữ liệu DB)
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            await _videoService.DeleteAsync(userId, id);
            return NoContent();
        }

        /// <summary>
        /// API Sinh video tự động bằng AI theo chủ đề và Content DNA
        /// </summary>
        [HttpPost("generate")]
        public async Task<IActionResult> Generate([FromBody] VideoGenerateRequest request)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var result = await _videoService.GenerateAsync(userId, request);
            return Accepted(result);
        }
    }
}
