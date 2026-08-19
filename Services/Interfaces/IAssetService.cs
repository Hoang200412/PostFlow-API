using System.IO;
using System.Threading.Tasks;
using PostFlow.Models.DTOs.Requests;

namespace PostFlow.Services.Interfaces
{
    public interface IAssetService
    {
        Task<object> GetPresignedUploadUrlAsync(string userId, PresignedUploadRequest request, string scheme, string host);
        Task<object> UploadBinaryAsync(string videoId, Stream bodyStream);
        Task<object> UploadConfirmAsync(string userId, UploadConfirmRequest request);
    }
}
