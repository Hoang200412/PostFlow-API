using System.Collections.Generic;
using System.Linq;
using PostFlow.Models.DTOs.Responses;

namespace PostFlow.Models.DTOs
{
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
