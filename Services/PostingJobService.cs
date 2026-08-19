using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using PostFlow.Data;
using PostFlow.Exceptions;
using PostFlow.Models;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Services.Interfaces;

namespace PostFlow.Services
{
    public class PostingJobService : IPostingJobService
    {
        private readonly AppDbContext _context;
        private readonly IBackgroundJobClient _backgroundJobClient;

        public PostingJobService(AppDbContext context, IBackgroundJobClient backgroundJobClient)
        {
            _context = context;
            _backgroundJobClient = backgroundJobClient;
        }

        public async Task<IEnumerable<object>> GetListAsync(string userId, UploadStatus? status, string? channelId)
        {
            var query = _context.UploadJobs
                .Where(j => j.UserId == userId);

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

        public async Task<UploadJob> UpdateAsync(string userId, string id, PostingJobUpdateRequest request)
        {
            var job = await _context.UploadJobs
                .Include(j => j.VideoAsset)
                .FirstOrDefaultAsync(j => j.Id == id && j.UserId == userId);

            if (job == null)
            {
                throw new NotFoundException($"Không tìm thấy Posting Job với ID {id}");
            }

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
                _backgroundJobClient.Schedule<IVideoUploadService>(s => s.ProcessUploadJobAsync(job.Id, System.Threading.CancellationToken.None), delay);
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
                .FirstOrDefaultAsync(j => j.Id == id && j.UserId == userId);

            if (job == null)
            {
                throw new NotFoundException($"Không tìm thấy Posting Job với ID {id}");
            }

            if (job.Status != UploadStatus.Pending)
            {
                throw new ConflictException("Không thể hủy job đã hoàn thành hoặc đang xử lý.");
            }

            job.Status = UploadStatus.Cancelled;
            await _context.SaveChangesAsync();
        }
    }
}
