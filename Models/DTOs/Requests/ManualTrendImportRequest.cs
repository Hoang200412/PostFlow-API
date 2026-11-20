using System;
using System.ComponentModel.DataAnnotations;

namespace PostFlow.Models.DTOs.Requests
{
    /// <summary>Thêm một video bằng link. Dùng cho TikTok/Reels (không có API công khai) hoặc video YouTube cụ thể.</summary>
    public class ManualTrendImportRequest
    {
        [Required(ErrorMessage = "Vui lòng nhập link video.")]
        [MaxLength(1000)]
        public string Url { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng chọn danh mục.")]
        public string Category { get; set; } = string.Empty;

        /// <summary>Bắt buộc khi oEmbed không trả về tiêu đề (ví dụ Facebook chưa cấu hình app).</summary>
        [MaxLength(500)]
        public string? Title { get; set; }

        /// <summary>Số liệu do người dùng đọc trên nền tảng. Bỏ qua với YouTube vì đã lấy từ API.</summary>
        [Range(0, long.MaxValue)]
        public long? Views { get; set; }

        [Range(0, long.MaxValue)]
        public long? Likes { get; set; }

        [Range(0, long.MaxValue)]
        public long? Comments { get; set; }

        /// <summary>Thời điểm đăng nếu biết. Với TikTok hệ thống tự suy ra từ id video.</summary>
        public DateTime? PublishedAt { get; set; }
    }
}
