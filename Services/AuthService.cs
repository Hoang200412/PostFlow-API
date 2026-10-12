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
using PostFlow.Exceptions;
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

            // Dọn dẹp token hết hạn hoặc quá giới hạn phiên của user
            await CleanupExpiredTokensAsync(user.Id);

            // Sinh Access Token mới và Refresh Token mới
            var token = GenerateJwtToken(user);
            var refreshToken = GenerateRefreshToken();

            var rt = new RefreshToken
            {
                UserId = user.Id,
                TokenHash = ComputeHash(refreshToken),
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedAt = DateTime.UtcNow
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

        public async Task<AuthResponseDto> LoginExternalAsync(ExternalIdentity identity)
        {
            if (string.IsNullOrWhiteSpace(identity.ProviderUserId))
            {
                throw new ArgumentException("Không nhận được định danh tài khoản mạng xã hội.");
            }

            var externalAccount = await _context.SocialAccounts
                .Include(account => account.User)
                .FirstOrDefaultAsync(account =>
                    account.Platform == identity.Platform &&
                    account.ChannelIdOrPageId == identity.ProviderUserId);

            var user = externalAccount?.User;
            if (user == null && !string.IsNullOrWhiteSpace(identity.Email))
            {
                user = await _context.Users.FirstOrDefaultAsync(candidate =>
                    candidate.Email.ToLower() == identity.Email.ToLower());
            }

            if (user == null)
            {
                var email = string.IsNullOrWhiteSpace(identity.Email)
                    ? $"{identity.Provider}_{identity.ProviderUserId}@oauth.postflow.local"
                    : identity.Email.Trim().ToLowerInvariant();
                var username = await BuildExternalUsernameAsync(identity.DisplayName, identity.ProviderUserId);

                user = new User
                {
                    Username = username,
                    Email = email,
                    PasswordHash = null,
                    Role = "User",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Users.Add(user);
            }

            if (!user.IsActive)
            {
                throw new ArgumentException("Tài khoản này đang bị khóa.");
            }

            if (externalAccount == null)
            {
                externalAccount = new SocialAccount
                {
                    UserId = user.Id,
                    Platform = identity.Platform,
                    ChannelIdOrPageId = identity.ProviderUserId,
                    AccountName = string.IsNullOrWhiteSpace(identity.DisplayName)
                        ? identity.Provider
                        : identity.DisplayName,
                    CreatedAt = DateTime.UtcNow
                };
                _context.SocialAccounts.Add(externalAccount);
            }

            externalAccount.UserId = user.Id;
            externalAccount.AccountName = string.IsNullOrWhiteSpace(identity.DisplayName)
                ? externalAccount.AccountName
                : identity.DisplayName;
            externalAccount.AccessToken = identity.AccessToken;
            externalAccount.RefreshToken = identity.RefreshToken;
            externalAccount.TokenExpiresAt = identity.TokenExpiresAt;
            externalAccount.IsActive = true;

            await CleanupExpiredTokensAsync(user.Id);
            var refreshToken = GenerateRefreshToken();
            _context.RefreshTokens.Add(new RefreshToken
            {
                UserId = user.Id,
                TokenHash = ComputeHash(refreshToken),
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            return new AuthResponseDto
            {
                Token = GenerateJwtToken(user),
                RefreshToken = refreshToken,
                UserId = user.Id,
                Username = user.Username,
                Email = user.Email,
                Role = user.Role
            };
        }

        private async Task<string> BuildExternalUsernameAsync(string displayName, string providerUserId)
        {
            var normalized = new string((displayName ?? string.Empty)
                .Where(char.IsLetterOrDigit)
                .ToArray());
            var baseUsername = string.IsNullOrWhiteSpace(normalized) ? "user" : normalized.ToLowerInvariant();
            var username = baseUsername;
            var suffix = 1;

            while (await _context.Users.AnyAsync(user => user.Username.ToLower() == username.ToLower()))
            {
                username = $"{baseUsername}_{suffix++}";
            }

            return username;
        }

        public async Task<AuthResponseDto?> RefreshTokenAsync(string refreshToken)
        {
            // 1. Kiểm tra tính hợp lệ của Refresh Token từ Database
            var tokenHash = ComputeHash(refreshToken);
            var existingToken = await _context.RefreshTokens
                .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

            if (existingToken == null || existingToken.ExpiresAt <= DateTime.UtcNow)
            {
                throw new UnauthorizedException("Refresh Token không hợp lệ hoặc đã hết hạn. Vui lòng đăng nhập lại.");
            }

            // 2. Tìm User từ UserId gắn với Refresh Token
            var user = await _context.Users.FindAsync(existingToken.UserId);
            if (user == null)
            {
                throw new NotFoundException("Không tìm thấy thông tin người dùng.");
            }

            // 3. Sinh Access Token mới và xoay vòng Refresh Token (Token Rotation)
            var newAccessToken = GenerateJwtToken(user);
            var newRefreshToken = GenerateRefreshToken();

            // Xóa token cũ
            _context.RefreshTokens.Remove(existingToken);

            // Dọn dẹp các token hết hạn khác (nếu có)
            await CleanupExpiredTokensAsync(user.Id);

            // Thêm token mới
            var rt = new RefreshToken
            {
                UserId = user.Id,
                TokenHash = ComputeHash(newRefreshToken),
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedAt = DateTime.UtcNow
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

        private async Task CleanupExpiredTokensAsync(string userId)
        {
            // 1. Xóa các token đã hết hạn của user
            var expiredTokens = await _context.RefreshTokens
                .Where(t => t.UserId == userId && t.ExpiresAt <= DateTime.UtcNow)
                .ToListAsync();

            if (expiredTokens.Count > 0)
            {
                _context.RefreshTokens.RemoveRange(expiredTokens);
            }

            // 2. Giới hạn tối đa 5 phiên đăng nhập hoạt động song song cho 1 user
            var activeTokens = await _context.RefreshTokens
                .Where(t => t.UserId == userId && t.ExpiresAt > DateTime.UtcNow)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            if (activeTokens.Count >= 5)
            {
                var extraTokens = activeTokens.Skip(4).ToList();
                _context.RefreshTokens.RemoveRange(extraTokens);
            }
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
