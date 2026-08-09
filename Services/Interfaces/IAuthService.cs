using System.Threading.Tasks;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Models.DTOs.Responses;

namespace PostFlow.Services.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResponseDto?> RegisterAsync(RegisterRequest request);
        Task<AuthResponseDto?> LoginAsync(LoginRequest request);
        Task<AuthResponseDto?> RefreshTokenAsync(TokenRequestDto request);
        Task<bool> RevokeTokenAsync(string userId);
    }
}
