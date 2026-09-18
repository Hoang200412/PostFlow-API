using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PostFlow.Models;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Models.DTOs.Responses;

namespace PostFlow.Services.Interfaces
{
    public interface IPublishingService
    {
        Task<UploadJob> EnqueueJobAsync(
            string userId,
            string videoAssetId,
            string socialAccountId,
            DateTime? scheduledAt = null,
            string? platformMetadataOverride = null);

        Task<IEnumerable<object>> GetListAsync(string userId, UploadStatus? status, string? channelId);

        Task<UploadJobDto?> GetByIdAsync(string jobId);

        Task<UploadJob> UpdateAsync(string userId, string id, PostingJobUpdateRequest request);

        Task CancelAsync(string userId, string id);

        Task ProcessJobAsync(string jobId, CancellationToken cancellationToken);
    }
}
