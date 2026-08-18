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
    [Authorize]
    [ApiController]
    [Route("api/content-dna")]
    public class ContentDnaController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IStorageService _storageService;

        public ContentDnaController(AppDbContext context, IStorageService storageService)
        {
            _context = context;
            _storageService = storageService;
        }

        private string? GetCurrentUserId() => 
            User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        [HttpGet]
        public async Task<IActionResult> GetList()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var list = await _context.ContentDNAs
                .Where(c => c.UserId == userId)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            return Ok(list);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ContentDnaCreateDto dto)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            if (string.IsNullOrWhiteSpace(dto.BrandName))
            {
                return UnprocessableEntity(new { message = "Brand name cannot be empty." });
            }

            var supportedVoices = new[] { "vi-VN-HoaiMyNeural", "vi-VN-NamMinhNeural", "en-US-JennyNeural", "en-US-GuyNeural" };
            if (!supportedVoices.Contains(dto.VoiceProfile))
            {
                return BadRequest(new { message = "Giọng đọc không được hỗ trợ." });
            }

            var dna = new ContentDNA
            {
                UserId = userId,
                BrandName = dto.BrandName,
                VoiceProfile = dto.VoiceProfile,
                AvatarAssetPath = dto.AvatarAssetPath ?? string.Empty,
                ScriptStructure = dto.ScriptStructure ?? "Hook/Context/Mechanism/Twist"
            };

            _context.ContentDNAs.Add(dna);
            await _context.SaveChangesAsync();

            return Created($"/api/content-dna/{dna.Id}", dna);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] ContentDnaCreateDto dto)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var dna = await _context.ContentDNAs.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
            if (dna == null) return NotFound();

            if (string.IsNullOrWhiteSpace(dto.BrandName))
            {
                return UnprocessableEntity(new { message = "Brand name cannot be empty." });
            }

            var supportedVoices = new[] { "vi-VN-HoaiMyNeural", "vi-VN-NamMinhNeural", "en-US-JennyNeural", "en-US-GuyNeural" };
            if (!supportedVoices.Contains(dto.VoiceProfile))
            {
                return BadRequest(new { message = "Giọng đọc không được hỗ trợ." });
            }

            dna.BrandName = dto.BrandName;
            dna.VoiceProfile = dto.VoiceProfile;
            dna.AvatarAssetPath = dto.AvatarAssetPath ?? dna.AvatarAssetPath;
            dna.ScriptStructure = dto.ScriptStructure ?? dna.ScriptStructure;

            await _context.SaveChangesAsync();
            return Ok(dna);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var dna = await _context.ContentDNAs.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
            if (dna == null) return NotFound();

            var isReferenced = await _context.VideoAssets.AnyAsync(v => v.ContentDNAId == id);
            if (isReferenced)
            {
                return Conflict(new { message = "Không thể xóa Content DNA này vì đang có video liên kết sử dụng." });
            }

            _context.ContentDNAs.Remove(dna);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpPost("avatar")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadAvatar(IFormFile file)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "Vui lòng chọn file avatar hợp lệ." });
            }

            var ext = Path.GetExtension(file.FileName).ToLower();
            if (ext != ".png" && ext != ".gif")
            {
                return BadRequest(new { message = "Chỉ hỗ trợ định dạng PNG và GIF." });
            }

            var fileName = $"{Guid.NewGuid()}{ext}";
            using var stream = file.OpenReadStream();
            var relativePath = await _storageService.SaveFileAsync(stream, $"avatars/{userId}", fileName);

            return Ok(new { avatarPath = relativePath });
        }
    }

    public class ContentDnaCreateDto
    {
        public string BrandName { get; set; } = string.Empty;
        public string VoiceProfile { get; set; } = string.Empty;
        public string? AvatarAssetPath { get; set; }
        public string? ScriptStructure { get; set; }
    }
}
