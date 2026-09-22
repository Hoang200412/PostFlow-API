using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PostFlow.Data;
using PostFlow.Exceptions;
using PostFlow.Models;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Models.DTOs.Responses;
using PostFlow.Services.Interfaces;

namespace PostFlow.Services
{
    public class NotificationService : INotificationService
    {
        private readonly AppDbContext _context;

        public NotificationService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<NotificationDto> CreateNotificationAsync(
            string userId,
            string title,
            string message,
            NotificationType type = NotificationType.Info,
            string? actionUrl = null,
            object? metadata = null)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new BadRequestException("UserId không được để trống.");
            }

            string? metadataJson = null;
            if (metadata != null)
            {
                metadataJson = metadata is string str ? str : JsonSerializer.Serialize(metadata);
            }

            var notification = new Notification
            {
                UserId = userId,
                Title = title,
                Message = message,
                Type = type,
                ActionUrl = actionUrl,
                IsRead = false,
                CreatedAt = DateTime.UtcNow,
                Metadata = metadataJson
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            return notification.ToDto();
        }

        public async Task<NotificationListResponse> GetPagedByUserAsync(string userId, NotificationQueryParams queryParams)
        {
            var query = _context.Notifications
                .AsNoTracking()
                .Where(n => n.UserId == userId);

            if (queryParams.UnreadOnly == true)
            {
                query = query.Where(n => !n.IsRead);
            }

            var totalCount = await query.CountAsync();
            var unreadCount = await _context.Notifications
                .CountAsync(n => n.UserId == userId && !n.IsRead);

            var page = queryParams.Page <= 0 ? 1 : queryParams.Page;
            var limit = queryParams.Limit <= 0 ? 20 : queryParams.Limit;

            var items = await query
                .OrderByDescending(n => n.CreatedAt)
                .Skip((page - 1) * limit)
                .Take(limit)
                .ToListAsync();

            return new NotificationListResponse
            {
                Items = items.Select(n => n.ToDto()).ToList(),
                TotalCount = totalCount,
                UnreadCount = unreadCount,
                Page = page,
                Limit = limit
            };
        }

        public async Task<int> GetUnreadCountAsync(string userId)
        {
            return await _context.Notifications
                .CountAsync(n => n.UserId == userId && !n.IsRead);
        }

        public async Task<bool> MarkAsReadAsync(string userId, string notificationId)
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

            if (notification == null)
            {
                throw new NotFoundException($"Không tìm thấy thông báo với ID: {notificationId}");
            }

            if (!notification.IsRead)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }

            return true;
        }

        public async Task<bool> MarkAllAsReadAsync(string userId)
        {
            var unreadList = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();

            if (unreadList.Any())
            {
                foreach (var notif in unreadList)
                {
                    notif.IsRead = true;
                }
                await _context.SaveChangesAsync();
            }

            return true;
        }

        public async Task<bool> DeleteNotificationAsync(string userId, string notificationId)
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

            if (notification == null)
            {
                throw new NotFoundException($"Không tìm thấy thông báo với ID: {notificationId}");
            }

            _context.Notifications.Remove(notification);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
