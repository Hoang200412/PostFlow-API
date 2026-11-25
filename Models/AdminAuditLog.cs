using System;

namespace PostFlow.Models
{
    /// <summary>Nhật ký mọi thao tác làm thay đổi dữ liệu do quản trị viên thực hiện.</summary>
    public class AdminAuditLog
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string AdminId { get; set; } = string.Empty;
        public string AdminUsername { get; set; } = string.Empty;

        /// <summary>Mã hành động, ví dụ USER_DELETE, CHANNEL_TOGGLE, AI_QUOTA_RESET.</summary>
        public string Action { get; set; } = string.Empty;

        /// <summary>Loại đối tượng bị tác động: User, Channel, UploadJob, Video, Media, Setting.</summary>
        public string TargetType { get; set; } = string.Empty;
        public string? TargetId { get; set; }
        public string? TargetLabel { get; set; }

        /// <summary>JSON gồm trạng thái trước/sau và lý do (nếu có).</summary>
        public string? Metadata { get; set; }
        public string? IpAddress { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
