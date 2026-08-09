using System;

namespace PostFlow.Models.DTOs.Responses
{
    public class UploadJobDto
    {
        public string Id { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string VideoAssetId { get; set; } = string.Empty;
        public string SocialAccountId { get; set; } = string.Empty;
        
        public string Platform { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int ProgressPercentage { get; set; }
        public string? ErrorMessage { get; set; }
        public string? VideoUrl { get; set; }
        
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }

        // Extra info for client convenience
        public string? VideoTitle { get; set; }
        public string? AccountName { get; set; }
    }
}
