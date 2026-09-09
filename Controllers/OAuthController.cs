using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PostFlow.Exceptions;
using PostFlow.Models;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Models.DTOs.Responses;
using PostFlow.Services.Interfaces;

namespace PostFlow.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/oauth")]
    public class OAuthController : ControllerBase
    {
        private readonly IOAuthService _oauthService;

        public OAuthController(IOAuthService oauthService)
        {
            _oauthService = oauthService;
        }

        private string? GetCurrentUserId() =>
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        /// <summary>
        /// API Lấy URL xác thực OAuth để Frontend chuyển hướng người dùng tới Google / TikTok / Meta Facebook
        /// </summary>
        [HttpGet("{platform}/login-url")]
        public ActionResult<OAuthLoginUrlResponse> GetLoginUrl(
            [FromRoute] PlatformType platform, 
            [FromQuery] string redirectUri, 
            [FromQuery] string? state = null)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            if (string.IsNullOrWhiteSpace(redirectUri))
            {
                throw new BadRequestException("Tham số redirectUri là bắt buộc.");
            }

            var authUrl = _oauthService.GetAuthorizationUrl(platform, redirectUri, state ?? userId);
            return Ok(new OAuthLoginUrlResponse
            {
                Platform = platform,
                AuthUrl = authUrl
            });
        }

        /// <summary>
        /// API Trao đổi OAuth code sau khi người dùng đồng ý cấp quyền trên trang mạng xã hội
        /// </summary>
        [HttpPost("{platform}/callback")]
        public async Task<ActionResult<SocialAccountDto>> HandleCallback(
            [FromRoute] PlatformType platform, 
            [FromBody] OAuthCallbackRequest request)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.RedirectUri))
            {
                throw new BadRequestException("Code và RedirectUri không được để trống.");
            }

            var account = await _oauthService.HandleCallbackAsync(userId, platform, request.Code, request.RedirectUri);
            return Ok(account);
        }
    }
}
