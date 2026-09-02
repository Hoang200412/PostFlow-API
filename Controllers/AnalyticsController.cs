using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PostFlow.Services.Interfaces;

namespace PostFlow.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/analytics")]
    public class AnalyticsController : ControllerBase
    {
        private readonly IAnalyticsService _analyticsService;

        public AnalyticsController(IAnalyticsService analyticsService)
        {
            _analyticsService = analyticsService;
        }

        private string? GetCurrentUserId() =>
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        /// <summary>
        /// API Tổng hợp thống kê Analytics (Views, Likes, Shares, Comments, Saves) của người dùng theo khoảng thời gian
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetSummary([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var summary = await _analyticsService.GetSummaryAsync(userId, fromDate, toDate);
            return Ok(summary);
        }

        /// <summary>
        /// API Lấy thông tin chi tiết lịch sử Analytics của một Job cụ thể
        /// </summary>
        [HttpGet("{jobId}")]
        public async Task<IActionResult> GetJobDetails(string jobId)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var details = await _analyticsService.GetJobDetailsAsync(userId, jobId);
            return Ok(details);
        }
    }
}
