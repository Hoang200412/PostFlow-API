using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Models.DTOs.Responses;

namespace PostFlow.Services.Interfaces
{
    public interface IVideoAssetService
    {
        Task<PagedResult<VideoAssetDto>> GetListAsync(string userId, int page, int limit);
        Task<VideoAssetDto> GetByIdAsync(string userId, string id);
        Task<VideoAssetDto> UpdateAsync(string userId, string id, VideoUpdateRequest request);
        Task<VideoAssetDto> UploadVideoAsync(string userId, IFormFile file, string? title, string? description, List<string>? tags);
        Task DeleteAsync(string userId, string id);
        Task BatchDeleteAsync(string userId, List<string> ids);
        Task<object> GenerateAsync(string userId, VideoGenerateRequest request);

        // Binary direct stream upload methods
        Task<object> GetPresignedUploadUrlAsync(string userId, PresignedUploadRequest request, string scheme, string host);
        Task<object> UploadBinaryAsync(string videoId, Stream bodyStream);
        Task<object> UploadConfirmAsync(string userId, UploadConfirmRequest request);
    }
}
