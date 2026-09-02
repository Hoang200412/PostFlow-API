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
    [Route("api/channels")]
    public class ChannelsController : ControllerBase
    {
        private readonly IChannelService _channelService;

        public ChannelsController(IChannelService channelService)
        {
            _channelService = channelService;
        }

        private string? GetCurrentUserId() =>
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        /// <summary>
        /// API Lấy danh sách tài khoản/kênh mạng xã hội đã kết nối của người dùng
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetList()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var list = await _channelService.GetListAsync(userId);
            return Ok(list);
        }

        /// <summary>
        /// API Kết nối kênh/tài khoản mạng xã hội mới
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Connect([FromBody] ConnectChannelRequest request)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var channel = await _channelService.ConnectAsync(userId, request);
            return Created($"/api/channels/{channel.Id}", channel);
        }

        /// <summary>
        /// API Hủy kết nối kênh mạng xã hội
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Disconnect(string id)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            await _channelService.DisconnectAsync(userId, id);
            return NoContent();
        }
    }
}
