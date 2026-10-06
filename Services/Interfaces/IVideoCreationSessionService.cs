using System.Threading.Tasks;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Models.DTOs.Responses;

namespace PostFlow.Services.Interfaces
{
    public interface IVideoCreationSessionService
    {
        Task<CreationSessionDto?> GetSessionAsync(string userId);
        Task<CreationSessionDto> UpsertSessionAsync(string userId, UpsertCreationSessionRequest request);
        Task DeleteSessionAsync(string userId);
    }
}
