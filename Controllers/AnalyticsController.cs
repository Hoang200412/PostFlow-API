using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PostFlow.Data;

namespace PostFlow.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/analytics")]
    public class AnalyticsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AnalyticsController(AppDbContext context)
        {
            _context = context;
        }

        private string? GetCurrentUserId() =>
            User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        [HttpGet]
        public async Task<IActionResult> GetSummary([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var start = fromDate ?? DateTime.UtcNow.AddDays(-30);
            var end = toDate ?? DateTime.UtcNow;

            var snapshotsQuery = _context.Analytics
                .Include(a => a.UploadJob)
                .Where(a => a.UploadJob!.UserId == userId && a.CapturedAt >= start && a.CapturedAt <= end);

            var platformStats = await snapshotsQuery
                .GroupBy(a => a.UploadJob!.Platform)
                .Select(g => new
                {
                    Platform = g.Key.ToString(),
                    TotalViews = g.Max(x => x.Views),
                    TotalLikes = g.Max(x => x.Likes),
                    TotalShares = g.Max(x => x.Shares),
                    TotalComments = g.Max(x => x.Comments),
                    TotalSaves = g.Max(x => x.Saves)
                })
                .ToListAsync();

            var totalStats = new
            {
                TotalViews = platformStats.Sum(p => p.TotalViews),
                TotalLikes = platformStats.Sum(p => p.TotalLikes),
                TotalShares = platformStats.Sum(p => p.TotalShares),
                TotalComments = platformStats.Sum(p => p.TotalComments),
                TotalSaves = platformStats.Sum(p => p.TotalSaves)
            };

            return Ok(new
            {
                totalStats,
                byPlatform = platformStats,
                dateRange = new { from = start, to = end }
            });
        }

        [HttpGet("{jobId}")]
        public async Task<IActionResult> GetJobDetails(string jobId)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var jobExists = await _context.UploadJobs
                .AnyAsync(j => j.Id == jobId && j.UserId == userId);

            if (!jobExists) return NotFound();

            var list = await _context.Analytics
                .Where(a => a.UploadJobId == jobId)
                .OrderBy(a => a.CapturedAt)
                .ToListAsync();

            return Ok(list);
        }
    }
}
