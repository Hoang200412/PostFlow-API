using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Services.Interfaces;

namespace PostFlow.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/content-dna")]
    public class ContentDnaController : ControllerBase
    {
        private readonly IContentDnaService _contentDnaService;

        public ContentDnaController(IContentDnaService contentDnaService)
        {
            _contentDnaService = contentDnaService;
        }

        private string? GetCurrentUserId() =>
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        /// <summary>
        /// API Lấy danh sách hồ sơ Content DNA thương hiệu của người dùng
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetList()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var list = await _contentDnaService.GetListAsync(userId);
            return Ok(list);
        }

        /// <summary>
        /// API Tạo hồ sơ Content DNA thương hiệu mới
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ContentDnaCreateDto dto)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var dna = await _contentDnaService.CreateAsync(userId, dto);
            return Created($"/api/content-dna/{dna.Id}", dna);
        }

        /// <summary>
        /// API Cập nhật hồ sơ Content DNA thương hiệu theo ID
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] ContentDnaCreateDto dto)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var dna = await _contentDnaService.UpdateAsync(userId, id, dto);
            return Ok(dna);
        }

        /// <summary>
        /// API Xóa hồ sơ Content DNA thương hiệu theo ID
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            await _contentDnaService.DeleteAsync(userId, id);
            return NoContent();
        }

        /// <summary>
        /// API Tải lên hình ảnh avatar cho hồ sơ thương hiệu (Hỗ trợ PNG/GIF)
        /// </summary>
        [HttpPost("avatar")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadAvatar(IFormFile file)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var relativePath = await _contentDnaService.UploadAvatarAsync(userId, file);
            return Ok(new { avatarPath = relativePath });
        }
    }
}
