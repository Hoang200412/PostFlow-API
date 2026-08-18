using System;

namespace PostFlow.Models
{
    public class Analytics
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string UploadJobId { get; set; } = string.Empty;
        public int Views { get; set; }
        public int Likes { get; set; }
        public int Shares { get; set; }
        public int Comments { get; set; }
        public int Saves { get; set; }
        public DateTime CapturedAt { get; set; } = DateTime.UtcNow;

        // Navigation Property
        public virtual UploadJob? UploadJob { get; set; }
    }
}
