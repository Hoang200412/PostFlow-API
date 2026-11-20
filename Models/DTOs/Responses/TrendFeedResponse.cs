using System;
using System.Collections.Generic;

namespace PostFlow.Models.DTOs.Responses
{
    public class TrendFeedResponse
    {
        public List<TrendTopicDto> Items { get; set; } = new();
        public int TotalMatched { get; set; }

        /// <summary>Thời điểm lượt thu thập thành công gần nhất.</summary>
        public DateTime? LastCollectedAt { get; set; }

        /// <summary>Máy chủ có cấu hình YouTube API key hay không.</summary>
        public bool CollectorConfigured { get; set; }

        /// <summary>Giải thích khi không có dữ liệu hoặc khi nền tảng chỉ hỗ trợ nhập tay.</summary>
        public string? Notice { get; set; }
    }
}
