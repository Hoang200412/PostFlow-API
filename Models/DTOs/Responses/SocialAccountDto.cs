using System;
using PostFlow.Models;

namespace PostFlow.Models.DTOs.Responses
{
    public class SocialAccountDto
    {
        public string Id { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public PlatformType Platform { get; set; }
        public string AccountName { get; set; } = string.Empty;
        public string ChannelIdOrPageId { get; set; } = string.Empty;
        public DateTime? TokenExpiresAt { get; set; }
        public bool IsActive { get; set; }

        /// <summary>true khi token bị nền tảng từ chối và người dùng cần kết nối lại kênh.</summary>
        public bool NeedsReconnect { get; set; }
        public string? LastTokenError { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public static class SocialAccountMappingExtensions
    {
        public static SocialAccountDto ToDto(this SocialAccount account)
        {
            return new SocialAccountDto
            {
                Id = account.Id,
                UserId = account.UserId,
                Platform = account.Platform,
                AccountName = account.AccountName,
                ChannelIdOrPageId = account.ChannelIdOrPageId,
                TokenExpiresAt = account.TokenExpiresAt,
                IsActive = account.IsActive,
                NeedsReconnect = account.NeedsReconnect,
                LastTokenError = account.LastTokenError,
                CreatedAt = account.CreatedAt
            };
        }
    }
}
