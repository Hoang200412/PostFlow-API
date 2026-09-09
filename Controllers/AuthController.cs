using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PostFlow.Data;
using PostFlow.Models;
using PostFlow.Exceptions;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Models.DTOs.Responses;
using PostFlow.Services.Interfaces;

namespace PostFlow.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpGet("debug-users")]
        public async Task<IActionResult> DebugUsers([FromServices] AppDbContext context, [FromServices] Microsoft.AspNetCore.Identity.IPasswordHasher<User> passwordHasher)
        {
            var users = await context.Users.ToListAsync();
            var debugInfo = users.Select(u => {
                var testVerify = !string.IsNullOrEmpty(u.PasswordHash) 
                    ? passwordHasher.VerifyHashedPassword(u, u.PasswordHash, "Password123!").ToString()
                    : "NoHash";
                return new {
                    u.Id,
                    u.Username,
                    u.Email,
                    u.Role,
                    HasPassword = !string.IsNullOrEmpty(u.PasswordHash),
                    TestVerifyPassword123 = testVerify
                };
            }).ToList();
            return Ok(debugInfo);
        }

        /// <summary>
        /// API Đăng ký tài khoản mới
        /// </summary>
        [HttpPost("register")]
        public async Task<ActionResult<AuthResponseDto>> Register([FromBody] RegisterRequest request)
        {
            var response = await _authService.RegisterAsync(request);
            if (response == null)
            {
                throw new BadRequestException("Không thể đăng ký tài khoản.");
            }

            // Ghi đè Refresh Token vào HttpOnly Cookie
            SetRefreshTokenCookie(response.RefreshToken);

            return Ok(response);
        }

        /// <summary>
        /// API Đăng nhập tài khoản
        /// </summary>
        [HttpPost("login")]
        public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginRequest request)
        {
            var response = await _authService.LoginAsync(request);
            if (response == null)
            {
                throw new BadRequestException("Đăng nhập thất bại.");
            }

            // Ghi đè Refresh Token vào HttpOnly Cookie
            SetRefreshTokenCookie(response.RefreshToken);

            return Ok(response);
        }

        /// <summary>
        /// API Làm mới Access Token bằng Refresh Token (Token Rotation) từ Cookie
        /// </summary>
        [HttpPost("refresh")]
        public async Task<ActionResult<AuthResponseDto>> Refresh()
        {
            // 1. Đọc Refresh Token từ Cookie
            var refreshToken = Request.Cookies["refreshToken"];
            if (string.IsNullOrEmpty(refreshToken))
            {
                throw new BadRequestException("Không tìm thấy Refresh Token trong Cookie. Vui lòng đăng nhập lại.");
            }

            // 2. Đọc Access Token cũ từ Header Authorization (Bearer <token>)
            string? accessToken = null;
            var authHeader = Request.Headers["Authorization"].ToString();
            if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                accessToken = authHeader.Substring("Bearer ".Length).Trim();
            }

            if (string.IsNullOrEmpty(accessToken))
            {
                throw new BadRequestException("Không tìm thấy Access Token trong Header Authorization.");
            }

            var requestDto = new TokenRequestDto
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken
            };

            var response = await _authService.RefreshTokenAsync(requestDto);
            if (response == null)
            {
                throw new BadRequestException("Làm mới token thất bại.");
            }

            // Xoay vòng cookie Refresh Token mới
            SetRefreshTokenCookie(response.RefreshToken);

            return Ok(response);
        }

        /// <summary>
        /// API Đăng xuất tài khoản (Xóa Refresh Token khỏi DB và Clear Cookie)
        /// </summary>
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            var refreshToken = Request.Cookies["refreshToken"];
            if (!string.IsNullOrEmpty(refreshToken))
            {
                await _authService.LogoutAsync(refreshToken);
            }

            // Xóa Cookie ở client
            Response.Cookies.Delete("refreshToken");

            return NoContent();
        }

        /// <summary>
        /// API Thu hồi Refresh Token (Đăng xuất / Thu hồi)
        /// </summary>
        [HttpPost("revoke/{userId}")]
        public async Task<IActionResult> Revoke(string userId)
        {
            var result = await _authService.RevokeTokenAsync(userId);
            if (!result)
            {
                throw new NotFoundException($"Không tìm thấy người dùng với ID {userId} để thu hồi token.");
            }

            // Xóa Cookie ở client
            Response.Cookies.Delete("refreshToken");

            return Ok(new { message = "Đã thu hồi Refresh Token thành công (Đăng xuất)." });
        }

        // Helper cấu hình Cookie bảo mật
        private void SetRefreshTokenCookie(string refreshToken)
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true, // Ngăn mã JavaScript đọc/truy cập (Chống XSS)
                Secure = true,   // Chỉ gửi qua HTTPS
                SameSite = SameSiteMode.Lax, // Chống CSRF
                Expires = DateTime.UtcNow.AddDays(7) // Hạn sử dụng của Cookie trùng với Refresh Token
            };
            Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);
        }
    }
}
