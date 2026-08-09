using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PostFlow.Data;
using PostFlow.Models;
using PostFlow.Services.Interfaces;
using PostFlow.Models.DTOs;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Models.DTOs.Responses;

namespace PostFlow.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UploadController : ControllerBase
    {
        private readonly IVideoUploadService _uploadService;
        private readonly AppDbContext _context;

        public UploadController(IVideoUploadService uploadService, AppDbContext context)
        {
            _uploadService = uploadService;
            _context = context;
        }

        /// <summary>
        /// API test trạng thái server
        /// </summary>
        [HttpGet("ping")]
        public IActionResult Ping()
        {
            return Ok(new { message = "PostFlow API is working!", time = DateTime.UtcNow });
        }

        /// <summary>
        /// Tạo dữ liệu giả lập (Seed Data) bao gồm 1 User, các SocialAccounts và 1 VideoAsset
        /// Giúp lập trình viên có sẵn các ID để test API ngay lập tức
        /// </summary>
        [HttpPost("seed")]
        public async Task<IActionResult> SeedMockData()
        {
            // 1. Tạo mock user nếu chưa có
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == "mock_creator");
            if (user == null)
            {
                user = new User
                {
                    Username = "mock_creator",
                    Email = "creator@postflow.com",
                    PasswordHash = "hashed_password_here",
                    Role = "User"
                };
                _context.Users.Add(user);
                await _context.SaveChangesAsync();
            }

            // 2. Tạo mock social accounts cho user nếu chưa có
            var accounts = await _context.SocialAccounts.Where(a => a.UserId == user.Id).ToListAsync();
            if (!accounts.Any())
            {
                accounts = new List<SocialAccount>
                {
                    new() { UserId = user.Id, Platform = PlatformType.YouTube, AccountName = "Kênh Shorts Vlog", ChannelIdOrPageId = "UC_mock_channel_youtube" },
                    new() { UserId = user.Id, Platform = PlatformType.TikTok, AccountName = "Nick TikTok Giải Trí", ChannelIdOrPageId = "@mock_tiktok_user" },
                    new() { UserId = user.Id, Platform = PlatformType.FacebookReels, AccountName = "Trang Reels Hài Hước", ChannelIdOrPageId = "123456789_mock_page" }
                };
                _context.SocialAccounts.AddRange(accounts);
                await _context.SaveChangesAsync();
            }

            // 3. Tạo mock video asset nếu chưa có
            var video = await _context.VideoAssets.FirstOrDefaultAsync(v => v.UserId == user.Id);
            if (video == null)
            {
                video = new VideoAsset
                {
                    UserId = user.Id,
                    Title = "Video ngắn tự động hóa",
                    Description = "Video được sinh tự động bởi PostFlow AI",
                    Tags = new List<string> { "automation", "ai", "postflow" },
                    LocalFilePath = "C:\\path\\to\\your\\video.mp4",
                    DurationSeconds = 30,
                    Status = "Ready"
                };
                _context.VideoAssets.Add(video);
                await _context.SaveChangesAsync();
            }

            return Ok(new
            {
                message = "Seed dữ liệu giả lập thành công!",
                userId = user.Id,
                videoAssetId = video.Id,
                socialAccounts = accounts.Select(a => new { a.Id, a.Platform, a.AccountName })
            });
        }

        /// <summary>
        /// Lấy tất cả danh sách các Job upload
        /// </summary>
        [HttpGet("jobs")]
        public async Task<ActionResult<IEnumerable<UploadJobDto>>> GetAllJobs()
        {
            var jobs = await _uploadService.GetAllJobsAsync();
            return Ok(jobs.ToDtoList());
        }

        /// <summary>
        /// Lấy trạng thái của một Job cụ thể
        /// </summary>
        [HttpGet("jobs/{jobId}")]
        public async Task<ActionResult<UploadJobDto>> GetJobById(string jobId)
        {
            var job = await _uploadService.GetJobByIdAsync(jobId);
            if (job == null)
            {
                return NotFound(new { message = $"Không tìm thấy Job với ID {jobId}" });
            }
            return Ok(job.ToDto());
        }

        /// <summary>
        /// Đẩy một Job upload mới vào hàng đợi Hangfire dựa trên các ID khóa ngoại
        /// </summary>
        [HttpPost("enqueue")]
        public async Task<ActionResult<UploadJobDto>> EnqueueJob([FromBody] EnqueueUploadRequest request)
        {
            try
            {
                var job = await _uploadService.EnqueueUploadJobAsync(request.UserId, request.VideoAssetId, request.SocialAccountId);
                return AcceptedAtAction(nameof(GetJobById), new { jobId = job.Id }, job.ToDto());
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Lỗi hệ thống: {ex.Message}" });
            }
        }

        /// <summary>
        /// Upload video bằng cách truyền đường dẫn File Local (Tương thích ngược)
        /// API sẽ tự động tạo mock User/SocialAccount để chạy luồng mới
        /// </summary>
        [HttpPost("local-path")]
        public async Task<ActionResult<UploadJobDto>> UploadFromLocalPath([FromBody] LocalPathUploadRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.VideoFilePath))
            {
                return BadRequest(new { message = "Đường dẫn file video không được để trống." });
            }

            // Đảm bảo có mock data trong DB để làm việc
            var (userId, socialAccountId, videoAssetId) = await GetOrCreateDefaultTestIds(
                request.Platform, 
                request.VideoFilePath, 
                request.Metadata ?? new VideoMetadata());

            var job = await _uploadService.EnqueueUploadJobAsync(userId, videoAssetId, socialAccountId);
            return AcceptedAtAction(nameof(GetJobById), new { jobId = job.Id }, job.ToDto());
        }

        /// <summary>
        /// Upload trực tiếp file video từ Client lên (Tương thích ngược)
        /// API lưu file tạm, tự tạo mock User/SocialAccount rồi tiến hành enqueue
        /// </summary>
        [HttpPost("file")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<UploadJobDto>> UploadFile(
            [FromForm] IFormFile file,
            [FromForm] PlatformType platform,
            [FromForm] string title,
            [FromForm] string description,
            [FromForm] string privacyStatus = "public")
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "Vui lòng chọn file video hợp lệ." });
            }

            var tempDir = Path.Combine(Directory.GetCurrentDirectory(), "temp_uploads");
            if (!Directory.Exists(tempDir))
            {
                Directory.CreateDirectory(tempDir);
            }

            var tempFileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var tempFilePath = Path.Combine(tempDir, tempFileName);

            using (var stream = new FileStream(tempFilePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var metadata = new VideoMetadata
            {
                Title = string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(file.FileName) : title,
                Description = description,
                PrivacyStatus = privacyStatus
            };

            var (userId, socialAccountId, videoAssetId) = await GetOrCreateDefaultTestIds(platform, tempFilePath, metadata);

            var job = await _uploadService.EnqueueUploadJobAsync(userId, videoAssetId, socialAccountId);
            return AcceptedAtAction(nameof(GetJobById), new { jobId = job.Id }, job.ToDto());
        }

        // Hàm helper tương thích ngược giúp tự động sinh các thực thể test khi gọi API cũ
        private async Task<(string userId, string socialAccountId, string videoAssetId)> GetOrCreateDefaultTestIds(
            PlatformType platform, string filePath, VideoMetadata metadata)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == "mock_creator");
            if (user == null)
            {
                user = new User { Username = "mock_creator", Email = "creator@postflow.com", Role = "User" };
                _context.Users.Add(user);
                await _context.SaveChangesAsync();
            }

            var account = await _context.SocialAccounts.FirstOrDefaultAsync(a => a.UserId == user.Id && a.Platform == platform);
            if (account == null)
            {
                account = new SocialAccount
                {
                    UserId = user.Id,
                    Platform = platform,
                    AccountName = $"Mock {platform} Account",
                    ChannelIdOrPageId = $"mock_id_{platform.ToString().ToLower()}"
                };
                _context.SocialAccounts.Add(account);
                await _context.SaveChangesAsync();
            }

            var video = new VideoAsset
            {
                UserId = user.Id,
                Title = metadata.Title,
                Description = metadata.Description,
                Tags = metadata.Tags,
                LocalFilePath = filePath,
                Status = "Ready"
            };
            _context.VideoAssets.Add(video);
            await _context.SaveChangesAsync();

            return (user.Id, account.Id, video.Id);
        }
    }
}
