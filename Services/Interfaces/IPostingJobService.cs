using System.Collections.Generic;
using System.Threading.Tasks;
using PostFlow.Models;
using PostFlow.Models.DTOs.Requests;

namespace PostFlow.Services.Interfaces
{
    public interface IPostingJobService
    {
        Task<IEnumerable<object>> GetListAsync(string userId, UploadStatus? status, string? channelId);
        Task<UploadJob> UpdateAsync(string userId, string id, PostingJobUpdateRequest request);
        Task CancelAsync(string userId, string id);
    }
}
