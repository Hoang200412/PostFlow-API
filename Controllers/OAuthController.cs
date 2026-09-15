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
        /// API Lấy URL xác thực OAuth để Frontend chuyển hướng người dùng tới Google / TikTok / Meta Facebook.
        /// Backend tự động tạo state chứa danh tính người dùng và cấu hình redirect_uri trỏ về Backend Callback.
        /// </summary>
        [Authorize]
        [HttpGet("{platform}/login-url")]
        public ActionResult<OAuthLoginUrlResponse> GetLoginUrl(
            [FromRoute] PlatformType platform, 
            [FromQuery] string? returnUrl = null,
            [FromQuery] string? redirectUri = null)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            // Địa chỉ Frontend cần quay về sau khi Backend nhận callback và lưu DB xong
            var frontendReturnUrl = !string.IsNullOrWhiteSpace(returnUrl) 
                ? returnUrl 
                : (!string.IsNullOrWhiteSpace(redirectUri) ? redirectUri : "http://localhost:5173/accounts");

            // URL callback của Backend mà Google / TikTok / Meta sẽ redirect về
            var backendCallbackUrl = $"{Request.Scheme}://{Request.Host}/api/oauth/{platform}/callback";

            // Tạo State an toàn chứa UserId và Frontend Return URL
            var state = _oauthService.GenerateState(userId, frontendReturnUrl);

            var authUrl = _oauthService.GetAuthorizationUrl(platform, backendCallbackUrl, state);
            return Ok(new OAuthLoginUrlResponse
            {
                Platform = platform,
                AuthUrl = authUrl
            });
        }

        /// <summary>
        /// Endpoint nhận GET Callback trực tiếp từ OAuth Provider (Google, TikTok, Meta).
        /// Backend tự động đổi token, lưu vào DB và 302 Redirect người dùng về giao diện Frontend React.
        /// </summary>
        [AllowAnonymous]
        [HttpGet("{platform}/callback")]
        public async Task<IActionResult> HandleDirectCallback(
            [FromRoute] PlatformType platform,
            [FromQuery] string? code = null,
            [FromQuery] string? state = null,
            [FromQuery] string? error = null)
        {
            string userId = string.Empty;
            string returnUrl = "http://localhost:5173/accounts";

            if (!string.IsNullOrWhiteSpace(state))
            {
                try
                {
                    var decoded = _oauthService.DecodeState(state);
                    userId = decoded.UserId;
                    if (!string.IsNullOrWhiteSpace(decoded.ReturnUrl))
                    {
                        returnUrl = decoded.ReturnUrl;
                    }
                }
                catch
                {
                    // State không giải mã được
                }
            }

            var separator = returnUrl.Contains("?") ? "&" : "?";

            if (!string.IsNullOrWhiteSpace(error))
            {
                return Redirect($"{returnUrl}{separator}oauth_status=error&message={Uri.EscapeDataString($"Xác thực bị từ chối: {error}")}");
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                return Redirect($"{returnUrl}{separator}oauth_status=error&message={Uri.EscapeDataString("Không nhận được authorization code từ nhà cung cấp.")}");
            }

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Redirect($"{returnUrl}{separator}oauth_status=error&message={Uri.EscapeDataString("Không thể xác định phiên làm việc người dùng (state không hợp lệ).")}");
            }

            try
            {
                var backendCallbackUrl = $"{Request.Scheme}://{Request.Host}/api/oauth/{platform}/callback";
                var account = await _oauthService.HandleCallbackAsync(userId, platform, code, backendCallbackUrl);

                return Redirect($"{returnUrl}{separator}oauth_status=success&platform={platform}&accountName={Uri.EscapeDataString(account.AccountName ?? "")}");
            }
            catch (Exception ex)
            {
                return Redirect($"{returnUrl}{separator}oauth_status=error&message={Uri.EscapeDataString(ex.Message)}");
            }
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

            if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.RedirectUri))
            {
                throw new BadRequestException("Code và RedirectUri không được để trống.");
            }

            var account = await _oauthService.HandleCallbackAsync(userId, platform, request.Code, request.RedirectUri);
            return Ok(account);
        }
    }
}
