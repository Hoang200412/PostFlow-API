using System.Threading.Tasks;
using PostFlow.Models;
using PostFlow.Models.DTOs.Requests;

namespace PostFlow.Services.Interfaces
{
    public interface IVideoService
    {
        Task<object> GetListAsync(string userId, int page, int limit);
        Task<VideoAsset> GetByIdAsync(string userId, string id);
        Task DeleteAsync(string userId, string id);
        Task<object> GenerateAsync(string userId, VideoGenerateRequest request);
    }
}
