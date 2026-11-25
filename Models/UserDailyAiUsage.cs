using System;

namespace PostFlow.Models
{
    /// <summary>Số lượt gọi AI của một người dùng trong một ngày (theo giờ Việt Nam).</summary>
    public class UserDailyAiUsage
    {
        public string UserId { get; set; } = string.Empty;

        /// <summary>Ngày tính theo giờ Việt Nam (UTC+7).</summary>
        public DateOnly UsageDate { get; set; }

        public int RequestCount { get; set; }

        /// <summary>Số lượt admin tặng thêm riêng cho ngày này; tự mất khi sang ngày mới.</summary>
        public int BonusCount { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public virtual User? User { get; set; }
    }
}
