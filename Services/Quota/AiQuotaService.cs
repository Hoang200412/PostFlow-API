using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using PostFlow.Data;
using PostFlow.Exceptions;
using PostFlow.Models;
using PostFlow.Services.Interfaces;

namespace PostFlow.Services.Quota
{
    /// <summary>
    /// Đếm và giới hạn số lượt gọi AI mỗi ngày của người dùng. Ngày được tính theo giờ Việt Nam (UTC+7),
    /// nên hạn mức làm mới vào 00:00 giờ Việt Nam.
    /// </summary>
    public class AiQuotaService : IAiQuotaService
    {
        public const string TrialLimitSettingKey = "AI_DAILY_LIMIT_TRIAL";
        public const string ProLimitSettingKey = "AI_DAILY_LIMIT_PRO";

        public const int Unlimited = -1;
        private const int DefaultTrialLimit = 100;
        private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

        private readonly AppDbContext _context;
        private readonly INotificationService _notificationService;
        private readonly ILogger<AiQuotaService> _logger;

        public AiQuotaService(
            AppDbContext context,
            INotificationService notificationService,
            ILogger<AiQuotaService> logger)
        {
            _context = context;
            _notificationService = notificationService;
            _logger = logger;
        }

        /// <summary>Ngày hiện tại theo giờ Việt Nam.</summary>
        public static DateOnly TodayInVietnam() => DateOnly.FromDateTime(DateTime.UtcNow + VietnamOffset);

        /// <summary>Thời điểm UTC của 00:00 giờ Việt Nam ngày kế tiếp.</summary>
        public static DateTime NextResetUtc(DateOnly today) =>
            DateTime.SpecifyKind(today.AddDays(1).ToDateTime(TimeOnly.MinValue) - VietnamOffset, DateTimeKind.Utc);

        public async Task<AiQuotaStatus> GetStatusAsync(string userId)
        {
            var today = TodayInVietnam();
            var (plan, limit) = await ResolvePlanAndLimitAsync(userId);
            var (used, bonus) = await ReadUsageAsync(userId, today);
            return BuildStatus(plan, limit, used, bonus, today);
        }

        public async Task<AiQuotaConsumeResult> TryConsumeAsync(string userId)
        {
            var today = TodayInVietnam();
            var (plan, limit) = await ResolvePlanAndLimitAsync(userId);

            // Không giới hạn: vẫn ghi nhận số lượt để admin theo dõi, nhưng không bao giờ chặn.
            var effectiveLimit = limit == Unlimited ? int.MaxValue / 2 : limit;

            var used = await TryIncrementAsync(userId, today, effectiveLimit);
            var (currentUsed, bonus) = await ReadUsageAsync(userId, today);
            var status = BuildStatus(plan, limit, used ?? currentUsed, bonus, today);

            if (used == null) return new AiQuotaConsumeResult(false, status);

            await NotifyThresholdsAsync(userId, status);
            return new AiQuotaConsumeResult(true, status);
        }

        public async Task RefundAsync(string userId)
        {
            var today = TodayInVietnam();
            try
            {
                await _context.UserDailyAiUsages
                    .Where(u => u.UserId == userId && u.UsageDate == today && u.RequestCount > 0)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(u => u.RequestCount, u => u.RequestCount - 1)
                        .SetProperty(u => u.UpdatedAt, DateTime.UtcNow));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể hoàn lượt AI cho user {UserId}", userId);
            }
        }

        /// <summary>
        /// Một câu SQL duy nhất: tạo dòng hoặc cộng 1 lượt chỉ khi chưa vượt (hạn mức + lượt tặng).
        /// Nhờ vậy nhiều request song song không thể cùng lọt qua ngưỡng.
        /// Trả về số lượt đã dùng sau khi cộng, hoặc null nếu đã hết hạn mức.
        /// </summary>
        private async Task<int?> TryIncrementAsync(string userId, DateOnly today, int limit)
        {
            const string sql = @"
INSERT INTO ""UserDailyAiUsages"" (""UserId"", ""UsageDate"", ""RequestCount"", ""BonusCount"", ""UpdatedAt"")
SELECT @userId, @date, 1, 0, @now
WHERE @limit >= 1
ON CONFLICT (""UserId"", ""UsageDate"") DO UPDATE
SET ""RequestCount"" = ""UserDailyAiUsages"".""RequestCount"" + 1, ""UpdatedAt"" = @now
WHERE ""UserDailyAiUsages"".""RequestCount"" < @limit + ""UserDailyAiUsages"".""BonusCount""
RETURNING ""RequestCount"";";

            var connection = _context.Database.GetDbConnection();
            var openedHere = connection.State != ConnectionState.Open;
            if (openedHere) await connection.OpenAsync();
            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = sql;
                var currentTransaction = _context.Database.CurrentTransaction;
                if (currentTransaction != null) command.Transaction = currentTransaction.GetDbTransaction();

                command.Parameters.Add(new NpgsqlParameter("userId", NpgsqlDbType.Text) { Value = userId });
                command.Parameters.Add(new NpgsqlParameter("date", NpgsqlDbType.Date) { Value = today });
                command.Parameters.Add(new NpgsqlParameter("now", NpgsqlDbType.Timestamp) { Value = DateTime.UtcNow });
                command.Parameters.Add(new NpgsqlParameter("limit", NpgsqlDbType.Integer) { Value = limit });

                var scalar = await command.ExecuteScalarAsync();
                return scalar is int count ? count : null;
            }
            finally
            {
                if (openedHere) await connection.CloseAsync();
            }
        }

        private async Task<(int Used, int Bonus)> ReadUsageAsync(string userId, DateOnly today)
        {
            var usage = await _context.UserDailyAiUsages
                .AsNoTracking()
                .Where(u => u.UserId == userId && u.UsageDate == today)
                .Select(u => new { u.RequestCount, u.BonusCount })
                .FirstOrDefaultAsync();
            return (usage?.RequestCount ?? 0, usage?.BonusCount ?? 0);
        }

        /// <summary>Hạn mức gốc trong ngày: Admin không giới hạn → override riêng của user → cấu hình theo gói.</summary>
        private async Task<(string Plan, int Limit)> ResolvePlanAndLimitAsync(string userId)
        {
            var user = await _context.Users.AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new { u.Role, u.Plan, u.DailyAiLimitOverride })
                .FirstOrDefaultAsync()
                ?? throw new NotFoundException("Không tìm thấy thông tin người dùng.");

            if (string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase)) return (user.Plan, Unlimited);
            if (user.DailyAiLimitOverride.HasValue) return (user.Plan, NormalizeLimit(user.DailyAiLimitOverride.Value));

            var isPro = string.Equals(user.Plan, UserPlans.Pro, StringComparison.OrdinalIgnoreCase);
            var key = isPro ? ProLimitSettingKey : TrialLimitSettingKey;
            var raw = await _context.SystemSettings.AsNoTracking()
                .Where(s => s.Key == key)
                .Select(s => s.Value)
                .FirstOrDefaultAsync();

            if (int.TryParse(raw, out var parsed)) return (user.Plan, NormalizeLimit(parsed));
            return (user.Plan, isPro ? Unlimited : DefaultTrialLimit);
        }

        private static int NormalizeLimit(int value) => value < 0 ? Unlimited : value;

        private static AiQuotaStatus BuildStatus(string plan, int limit, int used, int bonus, DateOnly today)
        {
            var unlimited = limit == Unlimited;
            var remaining = unlimited ? Unlimited : Math.Max(0, limit + bonus - used);
            return new AiQuotaStatus(plan, limit, bonus, used, remaining, unlimited, NextResetUtc(today));
        }

        /// <summary>Gửi thông báo cho user khi chạm 80% và khi dùng hết hạn mức.</summary>
        private async Task NotifyThresholdsAsync(string userId, AiQuotaStatus status)
        {
            if (status.IsUnlimited) return;

            var total = status.Limit + status.Bonus;
            if (total <= 0) return;

            var warnAt = (int)Math.Ceiling(total * 0.8);
            try
            {
                if (status.Used == total)
                {
                    await _notificationService.CreateNotificationAsync(
                        userId,
                        "Đã hết lượt dùng AI hôm nay",
                        $"Bạn đã dùng hết {total} lượt AI trong ngày. Hạn mức sẽ được làm mới lúc 00:00.",
                        NotificationType.Warning,
                        "/settings?tab=account",
                        new { type = "AI_QUOTA_EXHAUSTED", limit = total });
                }
                else if (status.Used == warnAt && warnAt < total)
                {
                    await _notificationService.CreateNotificationAsync(
                        userId,
                        "Sắp hết lượt dùng AI hôm nay",
                        $"Bạn đã dùng {status.Used}/{total} lượt AI trong ngày.",
                        NotificationType.Info,
                        "/settings?tab=account",
                        new { type = "AI_QUOTA_NEAR_LIMIT", used = status.Used, limit = total });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể gửi thông báo hạn mức AI cho user {UserId}", userId);
            }
        }
    }
}
