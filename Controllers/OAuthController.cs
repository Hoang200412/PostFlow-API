using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PostFlow.Exceptions;
using PostFlow.Models;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Models.DTOs.Responses;
using PostFlow.Services.Interfaces;

namespace PostFlow.Controllers
{
    [ApiController]
    [Route("api/oauth")]
    public class OAuthController : ControllerBase
    {
        private readonly IOAuthService _oauthService;

        public OAuthController(IOAuthService oauthService)
        {
            _oauthService = oauthService;
        }

        [AllowAnonymous]
        [HttpGet("social/{provider}/login-url")]
        public ActionResult<OAuthLoginUrlResponse> GetSocialLoginUrl(
            [FromRoute] string provider,
            [FromQuery] string redirectUri)
        {
            if (string.IsNullOrWhiteSpace(redirectUri))
            {
                return BadRequest("Redirect URI không được để trống.");
            }

            var authUrl = _oauthService.GetSocialLoginAuthorizationUrl(provider, redirectUri, out _);
            return Ok(new { provider, authUrl });
        }

        [AllowAnonymous]
        [HttpPost("social/{provider}/callback")]
        public async Task<ActionResult<AuthResponseDto>> HandleSocialLoginCallback(
            [FromRoute] string provider,
            [FromBody] SocialLoginCallbackRequest request)
        {
            var response = await _oauthService.HandleSocialLoginCallbackAsync(
                provider,
                request.Code,
                request.RedirectUri,
                request.State);

            SetRefreshTokenCookie(response.RefreshToken);
            return Ok(response);
        }

        private void SetRefreshTokenCookie(string refreshToken)
        {
            Response.Cookies.Append("refreshToken", refreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Expires = DateTime.UtcNow.AddDays(7)
            });
        }

        private string? GetCurrentUserId() =>
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        /// <summary>
        /// API Lấy URL xác thực OAuth để Frontend chuyển hướng người dùng tới Google / TikTok / Meta Facebook.
        /// Redirect URI được cấu hình trỏ trực tiếp về Frontend Callback (mặc định: http://localhost:5173/oauth/callback).
        /// </summary>
        [Authorize]
        [HttpGet("{platform}/login-url")]
        public ActionResult<OAuthLoginUrlResponse> GetLoginUrl(
            [FromRoute] PlatformType platform, 
            [FromQuery] string? redirectUri = null,
            [FromQuery] string? returnUrl = null)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            // Địa chỉ Frontend Callback mà OAuth Provider sẽ redirect về (FE nhận code & state)
            var frontendCallbackUrl = !string.IsNullOrWhiteSpace(redirectUri) 
                ? redirectUri 
                : "http://localhost:5173/oauth/callback";

            // Tạo State chứa Platform và Return URL
            var state = _oauthService.GenerateState(platform, returnUrl);

            var authUrl = _oauthService.GetAuthorizationUrl(platform, frontendCallbackUrl, state);
            return Ok(new OAuthLoginUrlResponse
            {
                Platform = platform,
                AuthUrl = authUrl
            });
        }

        /// <summary>
        /// API dự phòng: Trao đổi OAuth code nếu gọi bằng phương thức POST từ Frontend
        /// </summary>
        [Authorize]
        [HttpPost("{platform}/callback")]
        public async Task<ActionResult<SocialAccountDto>> HandleCallback(
            [FromRoute] PlatformType platform, 
            [FromBody] OAuthCallbackRequest request)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var account = await _oauthService.HandleCallbackAsync(userId, platform, request.Code, request.RedirectUri, request.CodeVerifier);
            return Ok(account);
        }
    }
}
