using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using PostFlow.Data;
using PostFlow.Models;
using PostFlow.Services.Interfaces;

namespace PostFlow.Services
{
    public class VideoGenerationService : IVideoGenerationService
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public VideoGenerationService(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        public async Task<string> GenerateVideoAsync(string userId, string topic, string contentDnaId)
        {
            var dna = await _context.ContentDNAs.FindAsync(contentDnaId);
            if (dna == null)
            {
                throw new ArgumentException("Không tìm thấy cấu hình Content DNA.");
            }

            // Giả lập Pipeline xử lý tuần tự (mất tầm vài giây để mô phỏng chính xác)
            // Bước 1: Gọi Gemini API để sinh kịch bản (9 phân cảnh)
            await Task.Delay(1000);
            await LogApiUsageAsync(userId, "Gemini", 1, 0.015m);

            // Bước 2: Gọi edge-tts để tổng hợp giọng đọc
            await Task.Delay(1000);

            // Bước 3: Gọi Pexels API 18 lần lấy stock footage
            await Task.Delay(2000);
            await LogApiUsageAsync(userId, "Pexels", 18, 0.00m); // Pexels is free

            // Bước 4: FFmpeg Render (A/B Split + Avatar overlay + Final Mix)
            await Task.Delay(2000);

            // 3. Tạo thư mục và ghi file video MP4 giả lập
            var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var outputFolder = Path.Combine(webRoot, "generated-videos", userId);
            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            var videoId = Guid.NewGuid().ToString();
            var fileName = $"{videoId}.mp4";
            var filePath = Path.Combine(outputFolder, fileName);

            // Tạo header file mp4 giả lập cơ bản
            byte[] mockMp4Bytes = new byte[] {
                0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70, // ftyp
                0x6d, 0x70, 0x34, 0x32, 0x00, 0x00, 0x00, 0x00,
                0x6d, 0x70, 0x34, 0x32, 0x69, 0x73, 0x6f, 0x6d
            };
            await File.WriteAllBytesAsync(filePath, mockMp4Bytes);

            return $"/generated-videos/{userId}/{fileName}";
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
}
