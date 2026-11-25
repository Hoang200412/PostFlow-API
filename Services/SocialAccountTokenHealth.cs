using System;
using System.Linq;
using PostFlow.Exceptions;
using PostFlow.Models;

namespace PostFlow.Services
{
    /// <summary>
    /// Theo dõi tình trạng token của kênh mạng xã hội: đánh dấu "cần kết nối lại" khi nền tảng từ chối token
    /// (đã bị thu hồi/hết hạn) và gỡ cờ khi người dùng kết nối lại hoặc đăng bài thành công.
    /// </summary>
    public static class SocialAccountTokenHealth
    {
        private static readonly string[] AuthErrorMarkers =
        {
            "invalid_grant",
            "invalid_token",
            "token has been expired",
            "token has been revoked",
            "access_token_invalid",
            "refresh_token_invalid",
            "[code 190]",
            "error validating access token",
            "session has expired",
            "unauthorized"
        };

        public static bool IsAuthError(Exception ex)
        {
            if (ex is UnauthorizedException) return true;
            return IsAuthError(ex.Message);
        }

        public static bool IsAuthError(string? message)
        {
            if (string.IsNullOrWhiteSpace(message)) return false;
            return AuthErrorMarkers.Any(marker => message.Contains(marker, StringComparison.OrdinalIgnoreCase));
        }

        public static void MarkNeedsReconnect(SocialAccount account, string reason)
        {
            account.NeedsReconnect = true;
            account.LastTokenError = reason.Length > 500 ? reason[..500] : reason;
            account.TokenErrorAt = DateTime.UtcNow;
        }

        public static void Clear(SocialAccount account)
        {
            account.NeedsReconnect = false;
            account.LastTokenError = null;
            account.TokenErrorAt = null;
        }
    }
}
