using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PostFlow.Data;
using PostFlow.Models;
using PostFlow.Services.Interfaces;
using Hangfire;

namespace PostFlow.Services
{
    public class VideoUploadService : IVideoUploadService
    {
        private readonly AppDbContext _context;
        private readonly VideoUploaderFactory _uploaderFactory;
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly ILogger<VideoUploadService> _logger;

        public VideoUploadService(
            AppDbContext context,
            VideoUploaderFactory uploaderFactory,
            IBackgroundJobClient backgroundJobClient,
            ILogger<VideoUploadService> logger)
        {
            _context = context;
            _uploaderFactory = uploaderFactory;
            _backgroundJobClient = backgroundJobClient;
            _logger = logger;
        }

        public async Task<UploadJob> EnqueueUploadJobAsync(string userId, string videoAssetId, string socialAccountId, DateTime? scheduledAt = null, string? platformMetadataOverride = null)
        {
            var socialAccount = await _context.SocialAccounts.FindAsync(socialAccountId);
            if (socialAccount == null)
            {
                throw new ArgumentException($"Không tìm thấy tài khoản liên kết với ID {socialAccountId}");
            }

            var job = new UploadJob
            {
                UserId = userId,
                VideoAssetId = videoAssetId,
                SocialAccountId = socialAccountId,
                Platform = socialAccount.Platform,
                Status = UploadStatus.Pending,
                ProgressPercentage = 0,
                ScheduledAt = scheduledAt,
                PlatformMetadataOverride = platformMetadataOverride,
                CreatedAt = DateTime.UtcNow
            };

            // Lưu job vào PostgreSQL
            _context.UploadJobs.Add(job);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Đã lưu Job vào Database. JobId: {JobId}, Platform: {Platform}", job.Id, job.Platform);

            // Gửi job vào Hangfire để thực thi nền
            if (job.ScheduledAt.HasValue && job.ScheduledAt.Value > DateTime.UtcNow)
            {
                var delay = job.ScheduledAt.Value - DateTime.UtcNow;
                _backgroundJobClient.Schedule<IVideoUploadService>(s => s.ProcessUploadJobAsync(job.Id, CancellationToken.None), delay);
                _logger.LogInformation("Đã đặt lịch đăng cho Job: {JobId} chạy sau {Delay}", job.Id, delay);
            }
            else
            {
                _backgroundJobClient.Enqueue<IVideoUploadService>(s => s.ProcessUploadJobAsync(job.Id, CancellationToken.None));
                _logger.LogInformation("Đã enqueue Job chạy ngay: {JobId}", job.Id);
            }

            return job;
        }

        public async Task<UploadJob?> GetJobByIdAsync(string jobId)
        {
            return await _context.UploadJobs
                .Include(j => j.VideoAsset)
                .Include(j => j.SocialAccount)
                .FirstOrDefaultAsync(j => j.Id == jobId);
        }

        public async Task<IEnumerable<UploadJob>> GetAllJobsAsync()
        {
            return await _context.UploadJobs
                .Include(j => j.VideoAsset)
                .Include(j => j.SocialAccount)
                .OrderByDescending(j => j.CreatedAt)
                .ToListAsync();
        }

        public async Task ProcessUploadJobAsync(string jobId, CancellationToken cancellationToken)
        {
            var job = await _context.UploadJobs
                .Include(j => j.VideoAsset)
                .Include(j => j.SocialAccount)
                .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);

            if (job == null)
            {
                _logger.LogError("Không tìm thấy Job trong Database để xử lý. JobId: {JobId}", jobId);
                return;
            }

            if (job.Status == UploadStatus.Cancelled)
            {
                _logger.LogWarning("Job {JobId} đã bị hủy bởi người dùng, bỏ qua xử lý.", jobId);
                return;
            }

            if (job.VideoAsset == null || job.SocialAccount == null)
            {
                _logger.LogError("Thiếu thông tin VideoAsset hoặc SocialAccount cho JobId: {JobId}", jobId);
                job.Status = UploadStatus.Failed;
                job.ErrorMessage = "Thiếu thông tin VideoAsset hoặc SocialAccount.";
                job.CompletedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(CancellationToken.None);
                return;
            }

            _logger.LogInformation("Hangfire bắt đầu xử lý Job: {JobId}", jobId);
            job.Status = UploadStatus.Processing;
            await _context.SaveChangesAsync(cancellationToken);

            try
            {
                // Lấy uploader từ Factory
                var uploader = _uploaderFactory.GetUploader(job.Platform);

                // Khởi tạo Progress reporter
                var progressReporter = new Progress<int>(async percent =>
                {
                    job.ProgressPercentage = percent;
                    try
                    {
                        await _context.SaveChangesAsync(CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Không thể cập nhật tiến trình cho Job {JobId}: {Msg}", jobId, ex.Message);
                    }
                    _logger.LogDebug("Job {JobId} tiến trình: {Percent}%", jobId, percent);
                });

                // Chuyển đổi thông tin từ VideoAsset sang VideoMetadata cho uploader, áp dụng platform override nếu có
                var title = job.VideoAsset.Title;
                var description = job.VideoAsset.Description;
                var tags = job.VideoAsset.Tags;

                if (!string.IsNullOrEmpty(job.PlatformMetadataOverride))
                {
                    try
                    {
                        var overrideData = System.Text.Json.JsonSerializer.Deserialize<VideoMetadata>(job.PlatformMetadataOverride);
                        if (overrideData != null)
                        {
                            if (!string.IsNullOrEmpty(overrideData.Title)) title = overrideData.Title;
                            if (!string.IsNullOrEmpty(overrideData.Description)) description = overrideData.Description;
                            if (overrideData.Tags != null && overrideData.Tags.Count > 0) tags = overrideData.Tags;
                        }
                    }
                    catch (Exception jsonEx)
                    {
                        _logger.LogWarning("Lỗi parse PlatformMetadataOverride cho Job {JobId}: {Msg}", jobId, jsonEx.Message);
                    }
                }

                var metadata = new VideoMetadata
                {
                    Title = title,
                    Description = description,
                    Tags = tags
                };

                // Thực hiện upload thực tế bằng file từ VideoAsset và account của job
                var result = await uploader.UploadAsync(
                    job.VideoAsset.LocalFilePath, 
                    metadata, 
                    job.SocialAccount,
                    progressReporter, 
                    cancellationToken);

                if (result.IsSuccess)
                {
                    job.Status = UploadStatus.Completed;
                    job.VideoUrl = result.VideoUrl;
                    job.PlatformPostId = result.VideoId ?? Guid.NewGuid().ToString().Substring(0, 8);
                    job.ProgressPercentage = 100;
                    _logger.LogInformation("Job {JobId} hoàn thành xuất sắc! URL: {Url}", jobId, result.VideoUrl);
                }
                else
                {
                    throw new Exception(result.ErrorMessage ?? "Uploader trả về trạng thái thất bại.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xảy ra trong tiến trình nền Hangfire của Job: {JobId}", jobId);

                if (job.RetryCount < 5)
                {
                    job.RetryCount++;
                    double seconds = Math.Min(Math.Pow(2, job.RetryCount) * 5, 3600);
                    job.NextRetryAt = DateTime.UtcNow.AddSeconds(seconds);
                    job.Status = UploadStatus.Pending; // Trả về Pending để chạy lại
                    job.ErrorMessage = $"Lỗi: {ex.Message}. Sẽ retry lần {job.RetryCount} sau {seconds} giây.";

                    _backgroundJobClient.Schedule<IVideoUploadService>(
                        s => s.ProcessUploadJobAsync(job.Id, CancellationToken.None), 
                        TimeSpan.FromSeconds(seconds));

                    _logger.LogInformation("Đã lên lịch retry lần {Count} cho Job {JobId} sau {Seconds}s", job.RetryCount, job.Id, seconds);
                }
                else
                {
                    job.Status = UploadStatus.DeadLetter;
                    job.ErrorMessage = ex.Message;
                    job.ErrorDetails = ex.ToString();
                    _logger.LogError("Job {JobId} đã vượt quá số lần retry tối đa. Chuyển sang DEAD_LETTER.", job.Id);
                }
            }
            finally
            {
                if (job.Status == UploadStatus.Completed || job.Status == UploadStatus.Failed || job.Status == UploadStatus.DeadLetter)
                {
                    job.CompletedAt = DateTime.UtcNow;
                }
                await _context.SaveChangesAsync(CancellationToken.None);
            }
        }
    }
}
