using System.Collections.Generic;

namespace PostFlow.Models
{
    public class VideoMetadata
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new();
        
        /// <summary>
        /// Quyền riêng tư của video. Ví dụ: "public", "private", "unlisted"
        /// </summary>
        public string PrivacyStatus { get; set; } = "public";

        /// <summary>
        /// Metadata nâng cao hoặc các cấu hình riêng biệt tùy nền tảng (dưới dạng JSON/Key-Value)
        /// </summary>
        public Dictionary<string, string> AdditionalParameters { get; set; } = new();
    }
}
