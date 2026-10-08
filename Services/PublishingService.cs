using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PostFlow.Data;
using PostFlow.Exceptions;
using PostFlow.Models;
using PostFlow.Models.DTOs;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Models.DTOs.Responses;
using PostFlow.Services.Interfaces;

namespace PostFlow.Services
{
    public class PublishingService : IPublishingService
    {
        private readonly AppDbContext _context;
        private readonly IPlatformPublisherFactory _publisherFactory;
        private readonly IStorageService _storageService;
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly INotificationService _notificationService;
        private readonly ILogger<PublishingService> _logger;

        public PublishingService(
            AppDbContext context,
            IPlatformPublisherFactory publisherFactory,
            IStorageService storageService,
            IBackgroundJobClient backgroundJobClient,
            INotificationService notificationService,
            ILogger<PublishingService> logger)
        {
            _context = context;
            _publisherFactory = publisherFactory;
            _storageService = storageService;
            _backgroundJobClient = backgroundJobClient;
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task<UploadJob> EnqueueJobAsync(
            string userId,
            string videoAssetId,
            string socialAccountId,
            DateTime? scheduledAt = null,
            string? platformMetadataOverride = null)
        {
            var socialAccount = await _context.SocialAccounts.FindAsync(socialAccountId);
            if (socialAccount == null)
            {
                throw new NotFoundException($"Không tìm thấy tài khoản liên kết với ID {socialAccountId}");
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

            _context.UploadJobs.Add(job);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Đã tạo PublishingJob {JobId} cho Platform {Platform}", job.Id, job.Platform);

            if (job.ScheduledAt.HasValue && job.ScheduledAt.Value > DateTime.UtcNow)
            {
                var delay = job.ScheduledAt.Value - DateTime.UtcNow;
                _backgroundJobClient.Schedule<IPublishingService>(s => s.ProcessJobAsync(job.Id, CancellationToken.None), delay);
                _logger.LogInformation("Đã hẹn giờ chạy Job {JobId} sau {Delay}", job.Id, delay);
            }
            else
            {
                _backgroundJobClient.Enqueue<IPublishingService>(s => s.ProcessJobAsync(job.Id, CancellationToken.None));
                _logger.LogInformation("Đã đẩy Job {JobId} vào hàng đợi xuất bản ngay", job.Id);
            }

            return job;
        }

        public async Task<IEnumerable<object>> GetListAsync(string userId, UploadStatus? status, string? channelId)
        {
            var query = _context.UploadJobs.Where(j => j.UserId == userId);

            if (status.HasValue)
            {
                query = query.Where(j => j.Status == status.Value);
            }

            if (!string.IsNullOrEmpty(channelId))
            {
                query = query.Where(j => j.SocialAccountId == channelId);
            }

            var list = await query
                .Include(j => j.VideoAsset)
                .Include(j => j.SocialAccount)
                .OrderByDescending(j => j.CreatedAt)
                .ToListAsync();

            return list.Select(j => new
            {
                j.Id,
                j.Platform,
                j.Status,
                j.ProgressPercentage,
                j.ErrorMessage,
                j.VideoUrl,
                j.ScheduledAt,
                j.PlatformMetadataOverride,
                j.PlatformPostId,
                j.RetryCount,
                j.NextRetryAt,
                j.CreatedAt,
                j.CompletedAt,
                Video = j.VideoAsset == null ? null : new { j.VideoAsset.Id, j.VideoAsset.Title, j.VideoAsset.Status },
                Channel = j.SocialAccount == null ? null : new { j.SocialAccount.Id, j.SocialAccount.AccountName, j.SocialAccount.Platform }
            });
        }

        public async Task<UploadJobDto?> GetByIdAsync(string jobId)
        {
            var job = await _context.UploadJobs
                .Include(j => j.VideoAsset)
                .Include(j => j.SocialAccount)
                .FirstOrDefaultAsync(j => j.Id == jobId);

            return job?.ToDto();
        }

        public async Task<UploadJob> UpdateAsync(string userId, string id, PostingJobUpdateRequest request)
        {
            var job = await _context.UploadJobs
                .Include(j => j.VideoAsset)
                .FirstOrDefaultAsync(j => j.Id == id && j.UserId == userId)
                ?? throw new NotFoundException($"Không tìm thấy Job với ID {id}");

            if (job.Status != UploadStatus.Pending)
            {
                throw new ConflictException("Không thể chỉnh sửa job đã hoàn thành hoặc đang xử lý.");
            }

            if (request.ScheduledAt.HasValue)
            {
                if (request.ScheduledAt.Value < DateTime.UtcNow.AddMinutes(5))
                {
                    throw new BadRequestException("Thời gian đặt lịch phải sau ít nhất 5 phút.");
                }
                job.ScheduledAt = request.ScheduledAt.Value;

                var delay = job.ScheduledAt.Value - DateTime.UtcNow;
                _backgroundJobClient.Schedule<IPublishingService>(s => s.ProcessJobAsync(job.Id, CancellationToken.None), delay);
            }

            if (request.PlatformMetadataOverride != null)
            {
                job.PlatformMetadataOverride = request.PlatformMetadataOverride;
            }

            await _context.SaveChangesAsync();
            return job;
        }

        public async Task CancelAsync(string userId, string id)
        {
            var job = await _context.UploadJobs
                .FirstOrDefaultAsync(j => j.Id == id && j.UserId == userId)
                ?? throw new NotFoundException($"Không tìm thấy Job với ID {id}");

            if (job.Status != UploadStatus.Pending)
            {
                throw new ConflictException("Không thể hủy job đã hoàn thành hoặc đang xử lý.");
            }

            job.Status = UploadStatus.Cancelled;
            await _context.SaveChangesAsync();
        }

        [AutomaticRetry(Attempts = 0)]
        public async Task ProcessJobAsync(string jobId, CancellationToken cancellationToken)
        {
            var job = await _context.UploadJobs
                .Include(j => j.VideoAsset)
                .Include(j => j.SocialAccount)
                .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);

            if (job == null)
            {
                _logger.LogError("Không tìm thấy Job trong Database. JobId: {JobId}", jobId);
                return;
            }

            // 1. Kiểm tra tính Idempotent: Nếu đã hoàn thành hoặc đã có PlatformPostId/VideoUrl thì tuyệt đối không đăng lại!
            if (job.Status == UploadStatus.Completed || !string.IsNullOrEmpty(job.PlatformPostId) || !string.IsNullOrEmpty(job.VideoUrl))
            {
                _logger.LogInformation("Job {JobId} đã được xuất bản hoàn tất trước đó (Status={Status}, PostId={PostId}), bỏ qua không đăng lại.",
                    jobId, job.Status, job.PlatformPostId);
                return;
            }

            // 2. Kiểm tra nếu job đã bị hủy
            if (job.Status == UploadStatus.Cancelled)
            {
                _logger.LogWarning("Job {JobId} đã bị hủy, dừng xử lý.", jobId);
                return;
            }

            // 3. Kiểm tra nếu job đang được worker khác xử lý
            if (job.Status == UploadStatus.Processing)
            {
                _logger.LogWarning("Job {JobId} đang được xử lý bởi tiến trình khác, bỏ qua để tránh trùng lặp.", jobId);
                return;
            }

            // 4. Kiểm tra thời gian hẹn giờ (nếu job bị trigger sớm hơn lịch dự kiến do đổi lịch trước đó)
            if (job.ScheduledAt.HasValue && job.ScheduledAt.Value > DateTime.UtcNow.AddSeconds(30))
            {
                _logger.LogInformation("Job {JobId} chưa đến thời gian hẹn giờ ({ScheduledAt}), bỏ qua trigger sớm.", jobId, job.ScheduledAt.Value);
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

            _logger.LogInformation("Bắt đầu thực thi xuất bản Job {JobId}", jobId);
            job.Status = UploadStatus.Processing;
            await _context.SaveChangesAsync(cancellationToken);

            try
            {
                var publisher = _publisherFactory.GetPublisher(job.Platform);

                var progressReporter = new Progress<int>(async percent =>
                {
                    job.ProgressPercentage = percent;
                    try
                    {
                        await _context.SaveChangesAsync(CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Lỗi cập nhật tiến trình Job {JobId}: {Msg}", jobId, ex.Message);
                    }
                });

                var title = job.VideoAsset.Title;
                var description = job.VideoAsset.Description;
                var tags = job.VideoAsset.Tags;

                if (!string.IsNullOrEmpty(job.PlatformMetadataOverride))
                {
                    try
                    {
                        var overrideData = JsonSerializer.Deserialize<VideoMetadata>(job.PlatformMetadataOverride);
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

                string effectiveFilePath = job.VideoAsset.LocalFilePath;
                bool isTempFile = false;

                if (!File.Exists(effectiveFilePath))
                {
                    _logger.LogInformation("File video không tồn tại trên local disk. Đang tải tạm từ Cloudflare R2: {Path}", job.VideoAsset.LocalFilePath);
                    effectiveFilePath = await _storageService.DownloadToTempFileAsync(job.VideoAsset.LocalFilePath);
                    isTempFile = true;
                }

                try
                {
                    var result = await publisher.PublishAsync(
                        effectiveFilePath,
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
                        job.CompletedAt = DateTime.UtcNow;
                        job.ErrorMessage = null;
                        await _context.SaveChangesAsync(CancellationToken.None);
                        _logger.LogInformation("Job {JobId} xuất bản thành công! URL: {Url}", jobId, result.VideoUrl);
                    }
                    else
                    {
                        throw new Exception(result.ErrorMessage ?? "Publisher trả về trạng thái thất bại.");
                    }
                }
                finally
                {
                    if (isTempFile && File.Exists(effectiveFilePath))
                    {
                        try { File.Delete(effectiveFilePath); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                // Nếu trước đó đã kịp upload thành công rồi (có PlatformPostId hoặc Status = Completed) thì không retry đăng lại
                if (job.Status == UploadStatus.Completed || !string.IsNullOrEmpty(job.PlatformPostId))
                {
                    _logger.LogWarning(ex, "Lỗi sau khi đã đăng bài thành công cho Job {JobId}. Không retry đăng lại.", jobId);
                    return;
                }

                _logger.LogError(ex, "Lỗi xảy ra trong tiến trình xuất bản Job {JobId}", jobId);

                if (job.RetryCount < 3)
                {
                    job.RetryCount++;
                    double seconds = Math.Min(Math.Pow(2, job.RetryCount) * 10, 3600);
                    job.NextRetryAt = DateTime.UtcNow.AddSeconds(seconds);
                    job.Status = UploadStatus.Pending;
                    job.ErrorMessage = $"Lỗi: {ex.Message}. Sẽ retry lần {job.RetryCount} sau {seconds} giây.";

                    _backgroundJobClient.Schedule<IPublishingService>(
                        s => s.ProcessJobAsync(job.Id, CancellationToken.None),
                        TimeSpan.FromSeconds(seconds));
                }
                else
                {
                    job.Status = UploadStatus.DeadLetter;
                    job.ErrorMessage = ex.Message;
                    job.ErrorDetails = ex.ToString();
                    _logger.LogError("Job {JobId} vượt quá số lần retry. Chuyển DEAD_LETTER.", job.Id);
                }
            }
            finally
            {
                if (job.Status == UploadStatus.Completed || job.Status == UploadStatus.Failed || job.Status == UploadStatus.DeadLetter)
                {
                    job.CompletedAt = DateTime.UtcNow;
                }
                await _context.SaveChangesAsync(CancellationToken.None);

                // Gửi thông báo realtime qua NotificationService
                try
                {
                    if (job.Status == UploadStatus.Completed)
                    {
                        var videoTitle = job.VideoAsset?.Title ?? "Video";
                        await _notificationService.CreateNotificationAsync(
                            job.UserId,
                            "Đăng bài thành công",
                            $"Video '{videoTitle}' đã được đăng thành công lên kênh {job.Platform}.",
                            NotificationType.Success,
                            "/schedule",
                            new { jobId = job.Id, platform = job.Platform.ToString(), videoUrl = job.VideoUrl });
                    }
                    else if (job.Status == UploadStatus.DeadLetter || job.Status == UploadStatus.Failed)
                    {
                        var videoTitle = job.VideoAsset?.Title ?? "Video";
                        await _notificationService.CreateNotificationAsync(
                            job.UserId,
                            "Đăng bài thất bại",
                            $"Không thể đăng video '{videoTitle}' lên {job.Platform}: {job.ErrorMessage}",
                            NotificationType.Error,
                            "/schedule",
                            new { jobId = job.Id, platform = job.Platform.ToString(), error = job.ErrorMessage });
                    }
                }
                catch (Exception notifEx)
                {
                    _logger.LogWarning("Không thể tạo thông báo cho Job {JobId}: {Msg}", jobId, notifEx.Message);
                }
            }
        }
    }
}
