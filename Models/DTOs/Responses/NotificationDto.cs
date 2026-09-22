using System;
using System.Collections.Generic;
using System.Linq;

namespace PostFlow.Models.DTOs.Responses
{
    public class NotificationDto
    {
        public string Id { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Type { get; set; } = "info";
        public string? ActionUrl { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? Metadata { get; set; }
    }

    public class NotificationListResponse
    {
        public List<NotificationDto> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int UnreadCount { get; set; }
        public int Page { get; set; }
        public int Limit { get; set; }
    }

    public static class NotificationMappingExtensions
    {
        public static NotificationDto ToDto(this Notification notification)
        {
            if (notification == null) return null!;

            return new NotificationDto
            {
                Id = notification.Id,
                UserId = notification.UserId,
                Title = notification.Title,
                Message = notification.Message,
                Type = notification.Type.ToString().ToLower(),
                ActionUrl = notification.ActionUrl,
                IsRead = notification.IsRead,
                CreatedAt = notification.CreatedAt,
                Metadata = notification.Metadata
            };
        }

        public static IEnumerable<NotificationDto> ToDtoList(this IEnumerable<Notification> notifications)
        {
            if (notifications == null) return Enumerable.Empty<NotificationDto>();
            return notifications.Select(n => n.ToDto());
        }
    }
}
