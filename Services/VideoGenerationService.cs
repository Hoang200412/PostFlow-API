using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PostFlow.Data;
using PostFlow.Models;
using PostFlow.Services.Interfaces;

namespace PostFlow.Services;

public class VideoGenerationService : IVideoGenerationService
{
    private readonly AppDbContext _context;
    public VideoGenerationService(
        AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Xử lý render và xuất video hoàn chỉnh đẩy lên Cloudflare R2
    /// </summary>
    public async Task<string> GenerateVideoAsync(
        string userId,
        string topic,
        string? contentDnaId,
        string scriptJson,
        string? voice)
    {
        if (!string.IsNullOrWhiteSpace(contentDnaId))
        {
            var dna = await _context.ContentDNAs.FindAsync(contentDnaId);
            if (dna == null)
            {
                throw new ArgumentException("Không tìm thấy cấu hình Content DNA.");
            }
        }

        throw new InvalidOperationException(
            "Video renderer chưa được cấu hình. Cần triển khai TTS, media composition và FFmpeg trước khi sinh video.");
    }

    private async Task LogApiUsageAsync(string userId, string provider, int callCount, decimal cost)
    {
        var today = DateTime.Today;
        var log = await _context.ApiUsageLogs
            .FirstOrDefaultAsync(l => l.UserId == userId && l.Provider == provider && l.Date == today);

        if (log == null)
        {
            log = new ApiUsageLog
            {
                UserId = userId,
                Provider = provider,
                CallCount = callCount,
                CostEstimate = cost,
                Date = today
            };
            _context.ApiUsageLogs.Add(log);
        }
        else
        {
            log.CallCount += callCount;
            log.CostEstimate += cost;
        }

        await _context.SaveChangesAsync();
    }
}
