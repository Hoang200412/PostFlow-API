using System.ComponentModel.DataAnnotations;

namespace PostFlow.Models.DTOs.Requests
{
    public class TrendFeedQuery
    {
        /// <summary>all, youtube, tiktok, reels.</summary>
        public string? Platform { get; set; }

        /// <summary>all hoặc một danh mục: tech, business, lifestyle, entertainment, education.</summary>
        public string? Category { get; set; }

        /// <summary>
        /// trending (mặc định: chart phổ biến + mẫu viral + nhập tay), popular, viral, control, manual, all.
        /// Mẫu đối chứng (control) chỉ trả về khi yêu cầu rõ, vì nó dùng cho phân tích chứ không phải để xem.
        /// </summary>
        public string? Sample { get; set; }

        /// <summary>Chỉ lấy Shorts.</summary>
        public bool ShortsOnly { get; set; }

        /// <summary>Chỉ lấy video đăng (hoặc được ghi nhận) trong N ngày gần nhất.</summary>
        [Range(1, 365)]
        public int Days { get; set; } = 30;

        [Range(1, 100)]
        public int Limit { get; set; } = 30;
    }
}
