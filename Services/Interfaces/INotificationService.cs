using System.Threading.Tasks;
using PostFlow.Models;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Models.DTOs.Responses;

namespace PostFlow.Services.Interfaces
{
    public interface INotificationService
    {
        Task<NotificationDto> CreateNotificationAsync(
            string userId,
            string title,
            string message,
            NotificationType type = NotificationType.Info,
            string? actionUrl = null,
            object? metadata = null);

        Task<NotificationListResponse> GetPagedByUserAsync(string userId, NotificationQueryParams queryParams);

        Task<int> GetUnreadCountAsync(string userId);

        Task<bool> MarkAsReadAsync(string userId, string notificationId);

        Task<bool> MarkAllAsReadAsync(string userId);

        Task<bool> DeleteNotificationAsync(string userId, string notificationId);
    }
}
