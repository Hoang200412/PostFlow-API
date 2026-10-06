using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Models.DTOs.Responses;
using PostFlow.Services.Interfaces;

namespace PostFlow.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/videos")]
    public class VideoController : ControllerBase
    {
        private readonly IVideoAssetService _videoAssetService;
        private readonly IScriptGenerationService _scriptGenService;
        private readonly IVideoCreationSessionService _sessionService;

        public VideoController(
            IVideoAssetService videoAssetService, 
            IScriptGenerationService scriptGenService,
            IVideoCreationSessionService sessionService)
        {
            _videoAssetService = videoAssetService;
            _scriptGenService = scriptGenService;
            _sessionService = sessionService;
        }

        private string? GetCurrentUserId() =>
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        /// <summary>
        /// API Lấy phiên tạo video dở gần nhất của người dùng
        /// </summary>
        [HttpGet("creation-session")]
        public async Task<IActionResult> GetCreationSession()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var session = await _sessionService.GetSessionAsync(userId);
            return Ok(new { hasActiveSession = session != null, session });
        }

        /// <summary>
        /// API Lưu hoặc cập nhật phiên tạo video của người dùng
        /// </summary>
        [HttpPut("creation-session")]
        public async Task<IActionResult> UpsertCreationSession([FromBody] UpsertCreationSessionRequest request)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var session = await _sessionService.UpsertSessionAsync(userId, request);
            return Ok(session);
        }

        /// <summary>
        /// API Xóa phiên tạo video của người dùng (khi reset/tạo mới)
        /// </summary>
        [HttpDelete("creation-session")]
        public async Task<IActionResult> DeleteCreationSession()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            await _sessionService.DeleteSessionAsync(userId);
            return NoContent();
        }

        /// <summary>
        /// API Lấy danh sách video tài sản của người dùng (Có phân trang)
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<PagedResult<VideoAssetDto>>> GetList([FromQuery] int page = 1, [FromQuery] int limit = 10)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var result = await _videoAssetService.GetListAsync(userId, page, limit);
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

            var video = await _videoAssetService.GetByIdAsync(userId, id);
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

            var updated = await _videoAssetService.UpdateAsync(userId, id, request);
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

            var video = await _videoAssetService.UploadVideoAsync(userId, request.File, request.Title, request.Description, request.GetTagList());
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

            await _videoAssetService.DeleteAsync(userId, id);
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

            await _videoAssetService.BatchDeleteAsync(userId, request.Ids);
            return NoContent();
        }

        /// <summary>
        /// API Sinh kịch bản video AI theo chủ đề và Content DNA (Structured JSON)
        /// </summary>
        [HttpPost("generate-script")]
        public async Task<IActionResult> GenerateScript([FromBody] GenerateScriptRequest request)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var script = await _scriptGenService.GenerateScriptAsync(userId, request);
            return Ok(script);
        }

        /// <summary>
        /// API Sinh video tự động bằng AI theo chủ đề và Content DNA
        /// </summary>
        [HttpPost("generate")]
        public async Task<IActionResult> Generate([FromBody] VideoGenerateRequest request)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var result = await _videoAssetService.GenerateAsync(userId, request);
            return Accepted(result);
        }

        /// <summary>
        /// API Lấy Presigned URL để chuẩn bị upload file video lớn
        /// </summary>
        [HttpPost("presigned-upload")]
        public async Task<IActionResult> GetPresignedUploadUrl([FromBody] PresignedUploadRequest request)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var result = await _videoAssetService.GetPresignedUploadUrlAsync(userId, request, Request.Scheme, Request.Host.ToString());
            return Ok(result);
        }

        /// <summary>
        /// API Tải luồng Binary file video lên hệ thống lưu trữ server
        /// </summary>
        [AllowAnonymous]
        [HttpPut("upload-binary/{videoId}")]
        public async Task<IActionResult> UploadBinary(string videoId)
        {
            var result = await _videoAssetService.UploadBinaryAsync(videoId, Request.Body);
            return Ok(result);
        }

        /// <summary>
        /// API Xác nhận tải video lên hoàn tất
        /// </summary>
        [HttpPost("upload-confirm")]
        public async Task<IActionResult> UploadConfirm([FromBody] UploadConfirmRequest request)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var result = await _videoAssetService.UploadConfirmAsync(userId, request);
            return Created($"/api/videos/{request.VideoId}", result);
        }
    }
}
