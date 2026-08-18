using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using PostFlow.Data;
using PostFlow.Models;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Models.DTOs.Responses;
using PostFlow.Services.Interfaces;

namespace PostFlow.Services
{
    public class AuthService : IAuthService
    {
        private readonly AppDbContext _context;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly IConfiguration _configuration;

        public AuthService(
            AppDbContext context,
            IPasswordHasher<User> passwordHasher,
            IConfiguration configuration)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _configuration = configuration;
        }

        public async Task<AuthResponseDto?> RegisterAsync(RegisterRequest request)
        {
            // Kiểm tra username đã tồn tại chưa
            if (await _context.Users.AnyAsync(u => u.Username.ToLower() == request.Username.ToLower()))
            {
                throw new ArgumentException("Tên đăng nhập đã tồn tại.");
            }

            // Kiểm tra email đã tồn tại chưa
            if (await _context.Users.AnyAsync(u => u.Email.ToLower() == request.Email.ToLower()))
            {
                throw new ArgumentException("Email đã đăng ký tài khoản khác.");
            }

            var user = new User
            {
                Username = request.Username,
                Email = request.Email,
                Role = "User",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            // Mã hóa mật khẩu
            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

            // Sinh Access Token và Refresh Token
            var token = GenerateJwtToken(user);
            var refreshToken = GenerateRefreshToken();

            // Lưu refresh token vào bảng RefreshTokens
            var rt = new RefreshToken
            {
                UserId = user.Id,
                TokenHash = ComputeHash(refreshToken),
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            };
            _context.RefreshTokens.Add(rt);

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return new AuthResponseDto
            {
                Token = token,
                RefreshToken = refreshToken,
                UserId = user.Id,
                Username = user.Username,
                Email = user.Email,
                Role = user.Role
            };
        }

        public async Task<AuthResponseDto?> LoginAsync(LoginRequest request)
        {
            // Tìm user theo username hoặc email
            var user = await _context.Users.FirstOrDefaultAsync(u => 
                u.Username.ToLower() == request.UsernameOrEmail.ToLower() || 
                u.Email.ToLower() == request.UsernameOrEmail.ToLower());

            if (user == null || string.IsNullOrEmpty(user.PasswordHash))
            {
                throw new ArgumentException("Tài khoản hoặc mật khẩu không chính xác.");
            }

            // Xác thực mật khẩu
            var verificationResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
            if (verificationResult == PasswordVerificationResult.Failed)
            {
                throw new ArgumentException("Tài khoản hoặc mật khẩu không chính xác.");
            }

            if (!user.IsActive)
            {
                throw new ArgumentException("Tài khoản này đang bị khóa.");
            }

            // Sinh Access Token mới và Refresh Token mới
            var token = GenerateJwtToken(user);
            var refreshToken = GenerateRefreshToken();

            var rt = new RefreshToken
            {
                UserId = user.Id,
                TokenHash = ComputeHash(refreshToken),
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            };
            _context.RefreshTokens.Add(rt);

            await _context.SaveChangesAsync();

            return new AuthResponseDto
            {
                Token = token,
                RefreshToken = refreshToken,
                UserId = user.Id,
                Username = user.Username,
                Email = user.Email,
                Role = user.Role
            };
        }

        public async Task<AuthResponseDto?> RefreshTokenAsync(TokenRequestDto request)
        {
            // 1. Trích xuất ClaimsPrincipal từ AccessToken đã hết hạn
            var principal = GetPrincipalFromExpiredToken(request.AccessToken);
            if (principal == null)
            {
                throw new ArgumentException("Access Token không hợp lệ.");
            }

            // 2. Tìm User ID từ claims
            var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException("Access Token thiếu thông tin định danh.");
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                throw new ArgumentException("Không tìm thấy người dùng.");
            }

            // 3. Kiểm tra tính hợp lệ của Refresh Token
            var tokenHash = ComputeHash(request.RefreshToken);
            var existingToken = await _context.RefreshTokens
                .FirstOrDefaultAsync(t => t.UserId == userId && t.TokenHash == tokenHash);

            if (existingToken == null || existingToken.ExpiresAt <= DateTime.UtcNow)
            {
                throw new ArgumentException("Refresh Token không hợp lệ hoặc đã hết hạn. Vui lòng đăng nhập lại.");
            }

            // 4. Sinh Access Token mới và xoay vòng Refresh Token (Token Rotation)
            var newAccessToken = GenerateJwtToken(user);
            var newRefreshToken = GenerateRefreshToken();

            // Xóa token cũ
            _context.RefreshTokens.Remove(existingToken);

            // Thêm token mới
            var rt = new RefreshToken
            {
                UserId = user.Id,
                TokenHash = ComputeHash(newRefreshToken),
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            };
            _context.RefreshTokens.Add(rt);

            await _context.SaveChangesAsync();

            return new AuthResponseDto
            {
                Token = newAccessToken,
                RefreshToken = newRefreshToken,
                UserId = user.Id,
                Username = user.Username,
                Email = user.Email,
                Role = user.Role
            };
        }

        public async Task<bool> RevokeTokenAsync(string userId)
        {
            var tokens = await _context.RefreshTokens.Where(t => t.UserId == userId).ToListAsync();
            if (!tokens.Any())
            {
                return false;
            }

            _context.RefreshTokens.RemoveRange(tokens);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> LogoutAsync(string refreshToken)
        {
            var tokenHash = ComputeHash(refreshToken);
            var token = await _context.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash);
            if (token == null)
            {
                return false;
            }

            _context.RefreshTokens.Remove(token);
            await _context.SaveChangesAsync();
            return true;
        }

        private string ComputeHash(string input)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            return Convert.ToBase64String(bytes);
        }

        private string GenerateJwtToken(User user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var keyStr = _configuration["JwtSettings:Key"];
            if (string.IsNullOrEmpty(keyStr))
            {
                throw new InvalidOperationException("Chưa cấu hình JWT Key trong appsettings.json.");
            }

            var key = Encoding.UTF8.GetBytes(keyStr);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id),
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Role, user.Role)
                }),
                Expires = DateTime.UtcNow.AddMinutes(double.Parse(_configuration["JwtSettings:ExpiryInMinutes"] ?? "180")),
                Issuer = _configuration["JwtSettings:Issuer"],
                Audience = _configuration["JwtSettings:Audience"],
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        private string GenerateRefreshToken()
        {
            var randomNumber = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }

        private ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
        {
            var keyStr = _configuration["JwtSettings:Key"];
            if (string.IsNullOrEmpty(keyStr))
            {
                throw new InvalidOperationException("Chưa cấu hình JWT Key trong appsettings.json.");
            }

            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = true,
                ValidateIssuer = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyStr)),
                ValidateLifetime = false, // Bỏ qua kiểm tra hạn sử dụng để đọc claims của token đã hết hạn
                ValidIssuer = _configuration["JwtSettings:Issuer"],
                ValidAudience = _configuration["JwtSettings:Audience"]
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            try
            {
                var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out SecurityToken securityToken);
                if (securityToken is not JwtSecurityToken jwtSecurityToken || 
                    !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
                {
                    return null;
                }

                return principal;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
