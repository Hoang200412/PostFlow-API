using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PostFlow.Data;
using PostFlow.Exceptions;
using PostFlow.Models;
using PostFlow.Services.Interfaces;

namespace PostFlow.Services
{
    public class AnalyticsService : IAnalyticsService
    {
        private readonly AppDbContext _context;

        public AnalyticsService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<object> GetSummaryAsync(string userId, DateTime? fromDate, DateTime? toDate)
        {
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

            return new
            {
                totalStats,
                byPlatform = platformStats,
                dateRange = new { from = start, to = end }
            };
        }

        public async Task<IEnumerable<Analytics>> GetJobDetailsAsync(string userId, string jobId)
        {
            var jobExists = await _context.UploadJobs
                .AnyAsync(j => j.Id == jobId && j.UserId == userId);

            if (!jobExists)
            {
                throw new NotFoundException($"Không tìm thấy dữ liệu Analytics cho Job ID {jobId}");
            }

            return await _context.Analytics
                .Where(a => a.UploadJobId == jobId)
                .OrderBy(a => a.CapturedAt)
                .ToListAsync();
        }
    }
}
