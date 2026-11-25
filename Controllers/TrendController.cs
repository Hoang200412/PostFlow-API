using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PostFlow.Filters;
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
        /// API Lấy danh sách video xu hướng từ dữ liệu đã thu thập (YouTube Data API và video người dùng thêm bằng link)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetTrendingList([FromQuery] TrendFeedQuery query)
        {
            var feed = await _trendService.GetTrendingTopicsAsync(query);
            return Ok(feed);
        }

        /// <summary>
        /// API Phân tích chuyên sâu xu hướng bằng AI (Tại sao nó trend, tâm lý người xem, 3 góc kịch bản)
        /// </summary>
        [HttpPost("analyze")]
        [RequireAiQuota]
        public async Task<IActionResult> AnalyzeTrend([FromBody] AnalyzeTrendRequest request)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var analysis = await _trendService.AnalyzeTrendAsync(userId, request);
            return Ok(analysis);
        }

        /// <summary>
        /// API Thêm video xu hướng bằng link (YouTube lấy số liệu từ API; TikTok/Reels lấy metadata oEmbed, số liệu nhập tay)
        /// </summary>
        [HttpPost("manual")]
        public async Task<IActionResult> ImportManual([FromBody] ManualTrendImportRequest request)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var video = await _trendService.ImportManualAsync(userId, request);
            return Ok(video);
        }

        /// <summary>
        /// API Lấy lịch sử số liệu (snapshot theo thời gian) của một video xu hướng
        /// </summary>
        [HttpGet("{id}/snapshots")]
        public async Task<IActionResult> GetSnapshots(string id)
        {
            var snapshots = await _trendService.GetSnapshotsAsync(id);
            return Ok(snapshots);
        }

        /// <summary>
        /// API Thống kê bộ dữ liệu xu hướng: số video, snapshot, quota đã dùng, các lượt thu thập gần nhất
        /// </summary>
        [HttpGet("collection/stats")]
        public async Task<IActionResult> GetCollectionStats()
        {
            var stats = await _trendService.GetCollectionStatsAsync();
            return Ok(stats);
        }

        /// <summary>
        /// API (Admin) Kích hoạt thu thập video xu hướng ngay (chạy nền qua Hangfire)
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpPost("collection/discover")]
        public IActionResult TriggerDiscover([FromQuery] string? category)
        {
            var result = _trendService.EnqueueDiscover(category);
            return Accepted(result);
        }

        /// <summary>
        /// API (Admin) Kích hoạt chụp snapshot số liệu cho các video đến hạn (chạy nền qua Hangfire)
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpPost("collection/snapshot")]
        public IActionResult TriggerSnapshot()
        {
            var result = _trendService.EnqueueSnapshot();
            return Accepted(result);
        }
    }
}
