using System.Threading.Tasks;
using PostFlow.Models;
using PostFlow.Models.DTOs.Responses;

namespace PostFlow.Services.Interfaces
{
    public interface IOAuthService
    {
        string GetAuthorizationUrl(PlatformType platform, string redirectUri, string state);
        string GenerateState(PlatformType platform, string? returnUrl = null);
        Task<SocialAccountDto> HandleCallbackAsync(string userId, PlatformType platform, string code, string redirectUri, string? codeVerifier = null);
        string GetSocialLoginAuthorizationUrl(string provider, string redirectUri, out string state);
        Task<AuthResponseDto> HandleSocialLoginCallbackAsync(string provider, string code, string redirectUri, string? state);
    }
}
