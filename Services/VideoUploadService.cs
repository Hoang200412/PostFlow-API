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

        public async Task<UploadJob> EnqueueUploadJobAsync(string userId, string videoAssetId, string socialAccountId)
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
                CreatedAt = DateTime.UtcNow
            };

            // Lưu job vào PostgreSQL
            _context.UploadJobs.Add(job);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Đã lưu Job vào Database. JobId: {JobId}, Platform: {Platform}", job.Id, job.Platform);

            // Gửi job vào Hangfire để thực thi nền đáng tin cậy
            _backgroundJobClient.Enqueue<IVideoUploadService>(s => s.ProcessUploadJobAsync(job.Id, CancellationToken.None));

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
            // Vì Hangfire chạy trong Background Scope riêng, chúng ta sẽ tìm Job từ DbContext
            var job = await _context.UploadJobs
                .Include(j => j.VideoAsset)
                .Include(j => j.SocialAccount)
                .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);

            if (job == null)
            {
                _logger.LogError("Không tìm thấy Job trong Database để xử lý. JobId: {JobId}", jobId);
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

                // Khởi tạo Progress reporter để cập nhật phần trăm tiến trình trực tiếp vào database
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

                // Chuyển đổi thông tin từ VideoAsset sang VideoMetadata cho uploader
                var metadata = new VideoMetadata
                {
                    Title = job.VideoAsset.Title,
                    Description = job.VideoAsset.Description,
                    Tags = job.VideoAsset.Tags
                };

                // Thực hiện upload thực tế bằng file từ VideoAsset
                var result = await uploader.UploadAsync(
                    job.VideoAsset.LocalFilePath, 
                    metadata, 
                    progressReporter, 
                    cancellationToken);

                if (result.IsSuccess)
                {
                    job.Status = UploadStatus.Completed;
                    job.VideoUrl = result.VideoUrl;
                    job.ProgressPercentage = 100;
                    _logger.LogInformation("Job {JobId} hoàn thành xuất sắc! URL: {Url}", jobId, result.VideoUrl);
                }
                else
                {
                    job.Status = UploadStatus.Failed;
                    job.ErrorMessage = result.ErrorMessage;
                    _logger.LogError("Job {JobId} thất bại. Lý do: {Error}", jobId, result.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                job.Status = UploadStatus.Failed;
                job.ErrorMessage = $"Lỗi xử lý nền: {ex.Message}";
                _logger.LogError(ex, "Lỗi xảy ra trong tiến trình nền Hangfire của Job: {JobId}", jobId);
            }
            finally
            {
                job.CompletedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(CancellationToken.None);
            }
        }
    }
}
