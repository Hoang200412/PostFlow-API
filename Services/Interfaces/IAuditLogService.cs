using System.Threading.Tasks;

namespace PostFlow.Services.Interfaces
{
    public interface IAuditLogService
    {
        /// <summary>
        /// Ghi một dòng vào nhật ký admin. Người thực hiện và địa chỉ IP lấy từ request hiện tại.
        /// </summary>
        Task LogAsync(string action, string targetType, string? targetId, string? targetLabel, object? metadata = null);
    }
}
