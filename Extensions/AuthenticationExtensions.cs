using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using PostFlow.Data;

namespace PostFlow.Extensions;

public static class AuthenticationExtensions
{
    private static readonly TimeSpan UserStatusCacheTtl = TimeSpan.FromSeconds(30);

    private sealed record UserStatus(bool IsActive, bool IsDeleted);

    public static IServiceCollection AddCorsPolicy(this IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", policy =>
            {
                policy.SetIsOriginAllowed(origin =>
                      {
                          if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                              return false;

                          return uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                              || uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
                              || uri.Host.EndsWith(".vercel.app", StringComparison.OrdinalIgnoreCase);
                      })
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .AllowCredentials();
            });
        });

        return services;
    }

    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSettings = configuration.GetSection("JwtSettings");
        var key = Encoding.UTF8.GetBytes(jwtSettings["Key"]!);

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings["Issuer"],
                ValidAudience = jwtSettings["Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ClockSkew = TimeSpan.Zero
            };

            // Access token sống 30 phút nên phải kiểm tra lại trạng thái user: khóa hoặc xóa tài khoản
            // có hiệu lực sau tối đa UserStatusCacheTtl thay vì chờ token hết hạn.
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                    if (string.IsNullOrEmpty(userId))
                    {
                        return;
                    }

                    var cache = context.HttpContext.RequestServices.GetRequiredService<IMemoryCache>();
                    var status = await cache.GetOrCreateAsync($"user-status:{userId}", async entry =>
                    {
                        entry.AbsoluteExpirationRelativeToNow = UserStatusCacheTtl;
                        var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                        return await db.Users.IgnoreQueryFilters()
                            .AsNoTracking()
                            .Where(u => u.Id == userId)
                            .Select(u => new UserStatus(u.IsActive, u.DeletedAt != null))
                            .FirstOrDefaultAsync();
                    });

                    if (status == null || !status.IsActive || status.IsDeleted)
                    {
                        context.Fail("Tài khoản đã bị khóa hoặc không còn tồn tại.");
                    }
                }
            };
        });

        return services;
    }
}
