using System;
using System.Collections.Generic;
using System.Linq;

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
        public DateTime? ScheduledAt { get; set; }
        public string? PlatformMetadataOverride { get; set; }
        public string? PlatformPostId { get; set; }
        public int RetryCount { get; set; }
        public DateTime? NextRetryAt { get; set; }
        
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }

        // Extra info for client convenience
        public string? VideoTitle { get; set; }
        public string? AccountName { get; set; }
    }

    public static class UploadJobMappingExtensions
    {
        public static UploadJobDto ToDto(this UploadJob job)
        {
            if (job == null) return null!;
            
            return new UploadJobDto
            {
                Id = job.Id,
                UserId = job.UserId,
                VideoAssetId = job.VideoAssetId,
                SocialAccountId = job.SocialAccountId,
                Platform = job.Platform.ToString(),
                Status = job.Status.ToString(),
                ProgressPercentage = job.ProgressPercentage,
                ErrorMessage = job.ErrorMessage,
                VideoUrl = job.VideoUrl,
                ScheduledAt = job.ScheduledAt,
                PlatformMetadataOverride = job.PlatformMetadataOverride,
                PlatformPostId = job.PlatformPostId,
                RetryCount = job.RetryCount,
                NextRetryAt = job.NextRetryAt,
                CreatedAt = job.CreatedAt,
                CompletedAt = job.CompletedAt,
                VideoTitle = job.VideoAsset?.Title,
                AccountName = job.SocialAccount?.AccountName
            };
        }

        public static IEnumerable<UploadJobDto> ToDtoList(this IEnumerable<UploadJob> jobs)
        {
            if (jobs == null) return Enumerable.Empty<UploadJobDto>();
            return jobs.Select(j => j.ToDto());
        }
    }
}
