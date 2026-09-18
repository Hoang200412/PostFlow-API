using System;
using System.Collections.Generic;

namespace PostFlow.Models
{
    public class VideoAsset
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        
        /// <summary>
        /// Danh sách từ khóa/thẻ tag của video.
        /// </summary>
        public List<string> Tags { get; set; } = new();
        
        /// <summary>
        /// Đường dẫn lưu file video thô vật lý trên máy chủ.
        /// </summary>
        public string LocalFilePath { get; set; } = string.Empty;
        public int DurationSeconds { get; set; }
        public string Status { get; set; } = "Ready"; // "Pending", "Rendering", "Ready", "Failed", "Uploading"
        public string? ContentDNAId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Navigation Properties cho Entity Framework Core
        public virtual User? User { get; set; }
        public virtual ContentDNA? ContentDNA { get; set; }
        public virtual ICollection<UploadJob> UploadJobs { get; set; } = new List<UploadJob>();
    }
}
