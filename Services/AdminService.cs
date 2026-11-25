using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PostFlow.Data;
using PostFlow.Exceptions;
using PostFlow.Models;
using PostFlow.Models.DTOs.Requests;
using System.Threading;
using Hangfire;
using PostFlow.Models.DTOs.Responses;
using PostFlow.Services.Interfaces;

namespace PostFlow.Services
{
    public class AdminService : IAdminService
    {
        private readonly AppDbContext _context;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly IBackgroundJobClient _backgroundJobClient;

        public AdminService(
            AppDbContext context,
            IPasswordHasher<User> passwordHasher,
            IBackgroundJobClient backgroundJobClient)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _backgroundJobClient = backgroundJobClient;
        }

        public async Task<IEnumerable<object>> GetUsersAsync()
        {
            var list = await _context.Users
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();

            return list.Select(u => new
            {
                u.Id,
                u.Username,
                u.Email,
                u.Role,
                u.IsActive,
                u.CreatedAt
            });
        }

        public async Task<object> CreateUserAsync(CreateUserRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                throw new BadRequestException("Username, Email, and Password are required.");
            }

            if (await _context.Users.IgnoreQueryFilters().AnyAsync(u => u.Username.ToLower() == request.Username.ToLower()))
            {
                throw new ConflictException("Username already exists.");
            }

            if (await _context.Users.IgnoreQueryFilters().AnyAsync(u => u.Email.ToLower() == request.Email.ToLower()))
            {
                throw new ConflictException("Email already exists.");
            }

            var user = new User
            {
                Username = request.Username,
                Email = request.Email,
                Role = request.Role ?? "User",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return new
            {
                user.Id,
                user.Username,
                user.Email,
                user.Role,
                user.IsActive,
                user.CreatedAt
            };
        }

        public async Task<object> GetUsageReportAsync(GetUsageReportRequest? request)
        {
            var targetDate = (request?.Date?.ToUniversalTime() ?? DateTime.UtcNow).Date;

            var usageLogs = await _context.ApiUsageLogs
                .Include(l => l.User)
                .Where(l => l.Date == targetDate)
                .ToListAsync();

            var summary = usageLogs
                .GroupBy(l => l.Provider)
                .Select(g => new
                {
                    Provider = g.Key,
                    TotalCalls = g.Sum(x => x.CallCount),
                    TotalCost = g.Sum(x => x.CostEstimate)
                });

            return new
            {
                date = targetDate,
                summary,
                details = usageLogs.Select(l => new
                {
                    l.Id,
                    l.Provider,
                    l.CallCount,
                    l.CostEstimate,
                    User = l.User == null ? null : new { l.User.Id, l.User.Username }
                })
            };
        }

        public async Task<SystemSetting> UpdateSettingAsync(string key, UpdateSettingRequest request)
        {
            if (request.Value == null)
            {
                throw new BadRequestException("Value cannot be null.");
            }

            var setting = await _context.SystemSettings.FindAsync(key);
            if (setting == null)
            {
                setting = new SystemSetting
                {
                    Key = key,
                    Value = request.Value,
                    Description = request.Description ?? string.Empty
                };
                _context.SystemSettings.Add(setting);
            }
            else
            {
                setting.Value = request.Value;
                if (request.Description != null)
                {
                    setting.Description = request.Description;
                }
            }

            await _context.SaveChangesAsync();
            return setting;
        }

        public async Task<PagedResult<object>> GetPublishingJobsAsync(AdminJobQueryParams queryParams)
        {
            var page = Math.Max(1, queryParams?.Page ?? 1);
            var limit = Math.Clamp(queryParams?.Limit ?? 20, 1, 100);
            var status = queryParams?.Status;
            var platform = queryParams?.Platform;
            var search = queryParams?.Search;

            var query = _context.UploadJobs
                .Include(j => j.VideoAsset)
                .Include(j => j.SocialAccount)
                .Include(j => j.User)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
            {
                if (Enum.TryParse<UploadStatus>(status, true, out var parsedStatus))
                {
                    query = query.Where(j => j.Status == parsedStatus);
                }
                else if (int.TryParse(status, out var intStatus) && Enum.IsDefined(typeof(UploadStatus), intStatus))
                {
                    query = query.Where(j => (int)j.Status == intStatus);
                }
            }

            if (!string.IsNullOrWhiteSpace(platform))
            {
                if (Enum.TryParse<PlatformType>(platform, true, out var parsedPlatform))
                {
                    query = query.Where(j => j.Platform == parsedPlatform);
                }
                else if (int.TryParse(platform, out var intPlatform) && Enum.IsDefined(typeof(PlatformType), intPlatform))
                {
                    query = query.Where(j => (int)j.Platform == intPlatform);
                }
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(j =>
                    (j.VideoAsset != null && j.VideoAsset.Title.ToLower().Contains(s)) ||
                    (j.SocialAccount != null && j.SocialAccount.AccountName.ToLower().Contains(s)) ||
                    (j.User != null && (j.User.Username.ToLower().Contains(s) || j.User.Email.ToLower().Contains(s))) ||
                    j.Id.Contains(s));
            }

            var total = await query.CountAsync();
            var jobs = await query
                .OrderByDescending(j => j.CreatedAt)
                .Skip((page - 1) * limit)
                .Take(limit)
                .ToListAsync();

            var items = jobs.Select(j => (object)new
            {
                j.Id,
                j.UserId,
                User = j.User == null ? null : new { j.User.Id, j.User.Username, j.User.Email },
                Platform = (int)j.Platform,
                PlatformName = j.Platform.ToString(),
                Status = (int)j.Status,
                StatusName = j.Status.ToString(),
                j.ProgressPercentage,
                j.ErrorMessage,
                j.VideoUrl,
                j.PlatformPostId,
                j.RetryCount,
                j.ScheduledAt,
                j.CreatedAt,
                j.CompletedAt,
                Video = j.VideoAsset == null ? null : new { j.VideoAsset.Id, j.VideoAsset.Title, j.VideoAsset.DurationSeconds },
                Channel = j.SocialAccount == null ? null : new { j.SocialAccount.Id, j.SocialAccount.AccountName, Platform = (int)j.SocialAccount.Platform }
            });

            return new PagedResult<object>(items, total, page, limit);
        }

        public async Task<object> CleanupStuckJobsAsync(CleanupStuckJobsRequest? request)
        {
            var stuckMinutes = Math.Max(1, request?.StuckMinutes ?? 15);
            var threshold = DateTime.UtcNow.AddMinutes(-stuckMinutes);
            var stuckJobs = await _context.UploadJobs
                .Where(j => j.Status == UploadStatus.Processing && j.CreatedAt <= threshold)
                .ToListAsync();

            foreach (var job in stuckJobs)
            {
                job.Status = UploadStatus.Failed;
                job.ErrorMessage = $"Tự động đánh dấu thất bại do kẹt tiến trình quá {stuckMinutes} phút (Server timeout / gián đoạn).";
                job.CompletedAt = DateTime.UtcNow;
            }

            if (stuckJobs.Count > 0)
            {
                await _context.SaveChangesAsync();
            }

            return new
            {
                cleanedCount = stuckJobs.Count,
                cleanedJobIds = stuckJobs.Select(j => j.Id).ToList()
            };
        }

        public async Task<bool> RetryJobAsync(string jobId)
        {
            var job = await _context.UploadJobs.FindAsync(jobId)
                ?? throw new NotFoundException($"Không tìm thấy Job {jobId}");

            job.Status = UploadStatus.Pending;
            job.ProgressPercentage = 0;
            job.RetryCount = 0;
            job.ErrorMessage = null;
            job.CompletedAt = null;
            await _context.SaveChangesAsync();

            _backgroundJobClient.Enqueue<IPublishingService>(s => s.ProcessJobAsync(jobId, CancellationToken.None));
            return true;
        }

        public async Task<bool> DeleteJobAsync(string jobId)
        {
            var job = await _context.UploadJobs.FindAsync(jobId)
                ?? throw new NotFoundException($"Không tìm thấy Job {jobId}");

            _context.UploadJobs.Remove(job);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
