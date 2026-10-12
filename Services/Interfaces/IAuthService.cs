using System.Threading.Tasks;
using PostFlow.Models;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Models.DTOs.Responses;

namespace PostFlow.Services.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResponseDto?> RegisterAsync(RegisterRequest request);
        Task<AuthResponseDto?> LoginAsync(LoginRequest request);
        Task<AuthResponseDto> LoginExternalAsync(ExternalIdentity identity);
        Task<AuthResponseDto?> RefreshTokenAsync(string refreshToken);
        Task<bool> RevokeTokenAsync(string userId);
        Task<bool> LogoutAsync(string refreshToken);
    }
}
