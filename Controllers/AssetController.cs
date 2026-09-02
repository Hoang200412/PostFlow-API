using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Services.Interfaces;

namespace PostFlow.Controllers
{
    [ApiController]
    [Route("api/assets")]
    public class AssetController : ControllerBase
    {
        private readonly IAssetService _assetService;

        public AssetController(IAssetService assetService)
        {
            _assetService = assetService;
        }

        private string? GetCurrentUserId() =>
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        /// <summary>
        /// API Lấy Presigned URL để chuẩn bị upload file video lớn
        /// </summary>
        [Authorize]
        [HttpPost("presigned-upload")]
        public async Task<IActionResult> GetPresignedUploadUrl([FromBody] PresignedUploadRequest request)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var result = await _assetService.GetPresignedUploadUrlAsync(userId, request, Request.Scheme, Request.Host.ToString());
            return Ok(result);
        }

        /// <summary>
        /// API Tải luồng Binary file video lên hệ thống lưu trữ server
        /// </summary>
        [HttpPut("upload-binary/{videoId}")]
        public async Task<IActionResult> UploadBinary(string videoId)
        {
            var result = await _assetService.UploadBinaryAsync(videoId, Request.Body);
            return Ok(result);
        }

        /// <summary>
        /// API Xác nhận tải video lên hoàn tất và tự động lên lịch/tải video lên các kênh social
        /// </summary>
        [Authorize]
        [HttpPost("upload-confirm")]
        public async Task<IActionResult> UploadConfirm([FromBody] UploadConfirmRequest request)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var result = await _assetService.UploadConfirmAsync(userId, request);
            return Created($"/api/videos/{request.VideoId}", result);
        }
    }
}
