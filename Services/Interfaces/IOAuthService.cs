using System.Threading.Tasks;
using PostFlow.Models;
using PostFlow.Models.DTOs.Responses;

namespace PostFlow.Services.Interfaces
{
    public interface IOAuthService
    {
        string GetAuthorizationUrl(PlatformType platform, string redirectUri, string state);
        Task<SocialAccountDto> HandleCallbackAsync(string userId, PlatformType platform, string code, string redirectUri);
    }
}
