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
        /// API Cập nhật thông tin video tài sản (Tiêu đề, mô tả, tags)
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] VideoUpdateRequest request)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var updated = await _videoService.UpdateAsync(userId, id, request);
            return Ok(updated);
        }

        /// <summary>
        /// API Tải lên file video trực tiếp vào thư viện
        /// </summary>
        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Upload([FromForm] VideoUploadRequest request)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var video = await _videoService.UploadVideoAsync(userId, request.File, request.Title, request.Description, request.GetTagList());
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
        /// API Xóa hàng loạt video tài sản theo danh sách ID
        /// </summary>
        [HttpDelete("batch")]
        public async Task<IActionResult> BatchDelete([FromBody] BatchDeleteRequest request)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            await _videoService.BatchDeleteAsync(userId, request.Ids);
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
