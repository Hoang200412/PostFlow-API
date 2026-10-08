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
    [Route("api/trends")]
    public class TrendController : ControllerBase
    {
        private readonly ITrendService _trendService;

        public TrendController(ITrendService trendService)
        {
            _trendService = trendService;
        }

        private string? GetCurrentUserId() =>
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        /// <summary>
        /// API Lấy danh sách các chủ đề xu hướng (Trending Topics) trên các mạng xã hội
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetTrendingList([FromQuery] string? platform, [FromQuery] string? category)
        {
            var trends = await _trendService.GetTrendingTopicsAsync(platform, category);
            return Ok(trends);
        }

        /// <summary>
        /// API Phân tích chuyên sâu xu hướng bằng AI (Tại sao nó trend, tâm lý người xem, 3 góc kịch bản)
        /// </summary>
        [HttpPost("analyze")]
        public async Task<IActionResult> AnalyzeTrend([FromBody] AnalyzeTrendRequest request)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var analysis = await _trendService.AnalyzeTrendAsync(userId, request);
            return Ok(analysis);
        }
    }
}
