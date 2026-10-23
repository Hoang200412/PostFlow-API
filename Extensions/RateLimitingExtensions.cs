using System;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PostFlow.Extensions;

public static class RateLimitingExtensions
{
    public const string AuthPolicy = "AuthRateLimiter";

    public static IServiceCollection AddCustomRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.ContentType = "application/json";

                var retryAfterSeconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                    ? (int)Math.Ceiling(retryAfter.TotalSeconds)
                    : 60;

                context.HttpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString();

                var responsePayload = new
                {
                    statusCode = StatusCodes.Status429TooManyRequests,
                    message = $"Bạn đã gửi quá nhiều yêu cầu đến hệ thống. Vui lòng thử lại sau {retryAfterSeconds} giây.",
                    retryAfterSeconds = retryAfterSeconds,
                    timestamp = DateTime.UtcNow
                };

                await context.HttpContext.Response.WriteAsJsonAsync(responsePayload, cancellationToken: token);
            };

            // 1. Global Limiter cho tất cả các endpoint (Fixed Window 100 requests / 1 phút theo User hoặc IP)
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var partitionKey = !string.IsNullOrEmpty(userId)
                    ? $"user_{userId}"
                    : $"ip_{GetClientIp(httpContext)}";

                return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 100, // Giới hạn 100 requests/phút
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                });
            });

            // 2. Policy riêng chống brute-force cho Authentication (login / register: 10 requests / 1 phút)
            options.AddPolicy(AuthPolicy, httpContext =>
            {
                var ip = GetClientIp(httpContext);
                return RateLimitPartition.GetFixedWindowLimiter($"auth_{ip}", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10, // 10 requests/phút
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                });
            });
        });

        return services;
    }

    private static string GetClientIp(HttpContext httpContext)
    {
        if (httpContext.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor) &&
            !string.IsNullOrWhiteSpace(forwardedFor))
        {
            var firstIp = forwardedFor.ToString().Split(',')[0].Trim();
            if (!string.IsNullOrEmpty(firstIp)) return firstIp;
        }

        return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
