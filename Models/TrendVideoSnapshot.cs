using System;

namespace PostFlow.Models
{
    /// <summary>Số liệu của một video tại một thời điểm. Dữ liệu chuỗi thời gian, chỉ thêm, không sửa.</summary>
    public class TrendVideoSnapshot
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string TrendVideoId { get; set; } = string.Empty;
        public DateTime CapturedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Tuổi video (giờ) tại thời điểm đo. Null khi không biết thời điểm đăng.</summary>
        public double? AgeHours { get; set; }

        /// <summary>Mốc tuổi chuẩn (24/72/168) mà snapshot này đại diện. Null với snapshot lúc phát hiện hoặc nhập tay.</summary>
        public int? MilestoneHours { get; set; }

        /// <summary>Null khi nền tảng ẩn số liệu (ví dụ ẩn like, tắt bình luận).</summary>
        public long? Views { get; set; }
        public long? Likes { get; set; }
        public long? Comments { get; set; }

        public string DataSource { get; set; } = TrendDataSources.YouTubeApi;

        // Navigation Property
        public virtual TrendVideo? TrendVideo { get; set; }
    }
}
