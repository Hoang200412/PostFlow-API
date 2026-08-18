using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PostFlow.Data;
using PostFlow.Models;

namespace PostFlow.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/channels")]
    public class ChannelsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ChannelsController(AppDbContext context)
        {
            _context = context;
        }

        private string? GetCurrentUserId() =>
            User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        [HttpGet]
        public async Task<IActionResult> GetList()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var list = await _context.SocialAccounts
                .Where(sa => sa.UserId == userId)
                .OrderBy(sa => sa.Platform)
                .ToListAsync();

            return Ok(list);
        }

        [HttpPost]
        public async Task<IActionResult> Connect([FromBody] ConnectChannelRequest request)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            if (string.IsNullOrWhiteSpace(request.AccountName) || string.IsNullOrWhiteSpace(request.ChannelIdOrPageId))
            {
                return BadRequest(new { message = "AccountName và ChannelIdOrPageId không được để trống." });
            }

            var exists = await _context.SocialAccounts
                .AnyAsync(sa => sa.UserId == userId && sa.Platform == request.Platform && sa.ChannelIdOrPageId == request.ChannelIdOrPageId);

            if (exists)
            {
                return Conflict(new { message = "Kênh này đã được kết nối trước đó." });
            }

            var channel = new SocialAccount
            {
                UserId = userId,
                Platform = request.Platform,
                AccountName = request.AccountName,
                ChannelIdOrPageId = request.ChannelIdOrPageId,
                AccessToken = Guid.NewGuid().ToString(),
                RefreshToken = Guid.NewGuid().ToString(),
                TokenExpiresAt = DateTime.UtcNow.AddDays(30),
                IsActive = true
            };

            _context.SocialAccounts.Add(channel);
            await _context.SaveChangesAsync();

            return Created($"/api/channels/{channel.Id}", channel);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Disconnect(string id)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var channel = await _context.SocialAccounts
                .FirstOrDefaultAsync(sa => sa.Id == id && sa.UserId == userId);

            if (channel == null) return NotFound();

            var hasActiveJobs = await _context.UploadJobs
                .AnyAsync(j => j.SocialAccountId == id && (j.Status == UploadStatus.Pending || j.Status == UploadStatus.Processing));

            if (hasActiveJobs)
            {
                return Conflict(new { message = "Không thể hủy kết nối kênh khi có bài đăng đang chờ hoặc đang tải lên." });
            }

            _context.SocialAccounts.Remove(channel);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }

    public class ConnectChannelRequest
    {
        public PlatformType Platform { get; set; }
        public string AccountName { get; set; } = string.Empty;
        public string ChannelIdOrPageId { get; set; } = string.Empty;
    }
}
