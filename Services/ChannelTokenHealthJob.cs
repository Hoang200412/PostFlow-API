using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PostFlow.Data;
using PostFlow.Models;
using PostFlow.Services.Interfaces;

namespace PostFlow.Services
{
    public class ChannelTokenHealthJob : IChannelTokenHealthJob
    {
        /// <summary>Cảnh báo sớm khi token không thể tự làm mới còn dưới số ngày này.</summary>
        public const int ExpiringSoonDays = 7;
        private static readonly TimeSpan NotificationCooldown = TimeSpan.FromDays(3);

        private readonly AppDbContext _context;
        private readonly INotificationService _notificationService;
        private readonly ILogger<ChannelTokenHealthJob> _logger;

        public ChannelTokenHealthJob(
            AppDbContext context,
            INotificationService notificationService,
            ILogger<ChannelTokenHealthJob> logger)
        {
            _context = context;
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task NotifyUnhealthyChannelsAsync()
        {
            var now = DateTime.UtcNow;
            var soon = now.AddDays(ExpiringSoonDays);

            // Kênh không có refresh token thì access token hết hạn là hết dùng được: chuyển sang "cần kết nối lại"
            var expired = await _context.SocialAccounts
                .Where(a => a.IsActive && !a.NeedsReconnect
                    && a.RefreshToken == ""
                    && a.TokenExpiresAt != null && a.TokenExpiresAt <= now)
                .ToListAsync();
            foreach (var account in expired)
            {
                SocialAccountTokenHealth.MarkNeedsReconnect(account, "Token đã hết hạn và kênh không có refresh token.");
            }
            if (expired.Count > 0) await _context.SaveChangesAsync();

            var needReconnect = await _context.SocialAccounts
                .Where(a => a.IsActive && a.NeedsReconnect)
                .ToListAsync();
            foreach (var account in needReconnect)
            {
                await NotifyOnceAsync(
                    account,
                    "Kênh cần kết nối lại",
                    $"Kênh {account.AccountName} ({account.Platform}) không còn quyền đăng bài. Hãy kết nối lại để tiếp tục đăng.",
                    NotificationType.Error,
                    "CHANNEL_NEEDS_RECONNECT");
            }

            var expiringSoon = await _context.SocialAccounts
                .Where(a => a.IsActive && !a.NeedsReconnect
                    && a.RefreshToken == ""
                    && a.TokenExpiresAt != null && a.TokenExpiresAt > now && a.TokenExpiresAt <= soon)
                .ToListAsync();
            foreach (var account in expiringSoon)
            {
                var days = Math.Max(1, (int)Math.Ceiling((account.TokenExpiresAt!.Value - now).TotalDays));
                await NotifyOnceAsync(
                    account,
                    "Token kênh sắp hết hạn",
                    $"Token của kênh {account.AccountName} ({account.Platform}) sẽ hết hạn sau khoảng {days} ngày. Hãy kết nối lại sớm để không bị gián đoạn.",
                    NotificationType.Warning,
                    "CHANNEL_TOKEN_EXPIRING");
            }

            _logger.LogInformation(
                "Quét token kênh: {Expired} kênh hết hạn, {Reconnect} kênh cần kết nối lại, {Soon} kênh sắp hết hạn.",
                expired.Count, needReconnect.Count, expiringSoon.Count);
        }

        private async Task NotifyOnceAsync(SocialAccount account, string title, string message, NotificationType type, string code)
        {
            var since = DateTime.UtcNow - NotificationCooldown;
            var alreadySent = await _context.Notifications.AnyAsync(n =>
                n.UserId == account.UserId
                && n.CreatedAt > since
                && n.Metadata != null
                && n.Metadata.Contains(account.Id)
                && n.Metadata.Contains(code));
            if (alreadySent) return;

            try
            {
                await _notificationService.CreateNotificationAsync(
                    account.UserId, title, message, type, "/accounts",
                    new { type = code, accountId = account.Id, platform = account.Platform.ToString() });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể gửi thông báo token cho kênh {AccountId}", account.Id);
            }
        }
    }
}
