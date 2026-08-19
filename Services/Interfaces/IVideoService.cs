using System.Threading.Tasks;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Models.DTOs.Responses;

namespace PostFlow.Services.Interfaces
{
    public interface IVideoService
    {
        Task<object> GetListAsync(string userId, int page, int limit);
        Task<VideoAssetDto> GetByIdAsync(string userId, string id);
        Task DeleteAsync(string userId, string id);
        Task<object> GenerateAsync(string userId, VideoGenerateRequest request);
    }
}
