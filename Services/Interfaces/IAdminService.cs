using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PostFlow.Models;
using PostFlow.Models.DTOs.Requests;

using PostFlow.Models.DTOs.Responses;

namespace PostFlow.Services.Interfaces
{
    public interface IAdminService
    {
        Task<IEnumerable<object>> GetUsersAsync();
        Task<object> CreateUserAsync(CreateUserRequest request);
        Task<object> GetUsageReportAsync(GetUsageReportRequest? request);
        Task<SystemSetting> UpdateSettingAsync(string key, UpdateSettingRequest request);
        Task<PagedResult<object>> GetPublishingJobsAsync(AdminJobQueryParams queryParams);
        Task<object> CleanupStuckJobsAsync(CleanupStuckJobsRequest? request);
        Task<bool> RetryJobAsync(string jobId);
        Task<bool> DeleteJobAsync(string jobId);
        Task<IEnumerable<TtsVoice>> GetVoicesAsync();
        Task<TtsVoice> CreateVoiceAsync(UpsertTtsVoiceRequest request);
        Task<TtsVoice> UpdateVoiceAsync(string id, UpsertTtsVoiceRequest request);
        Task<bool> DeleteVoiceAsync(string id);
        Task<TtsVoice> GenerateVoiceSampleAsync(string id);
        Task<int> GenerateMissingVoiceSamplesAsync();
    }
}
