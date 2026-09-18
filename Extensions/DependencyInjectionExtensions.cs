using Microsoft.AspNetCore.Identity;
using PostFlow.Configuration;
using PostFlow.Models;
using PostFlow.Services;
using PostFlow.Services.Interfaces;
using PostFlow.Services.Uploaders;

namespace PostFlow.Extensions;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Cấu hình URL luôn dùng chữ thường (kebab-case / lowercase)
        services.Configure<RouteOptions>(options =>
        {
            options.LowercaseUrls = true;
            options.LowercaseQueryStrings = true;
        });

        // Bind Platform API Settings from appsettings.json
        services.Configure<PlatformSettings>(configuration.GetSection("PlatformSettings"));

        // Register Platform-specific uploaders
        services.AddTransient<IVideoUploader, YouTubeUploader>();
        services.AddTransient<IVideoUploader, TikTokUploader>();
        services.AddTransient<IVideoUploader, FacebookReelsUploader>();

        // Register Factory & Publishing Service
        services.AddTransient<IPlatformPublisherFactory, PlatformPublisherFactory>();
        services.AddScoped<IPublishingService, PublishingService>();

        // Register Storage, Video Generation, and Background Jobs Services
        services.AddScoped<IStorageService, StorageService>();
        services.AddScoped<IVideoGenerationService, VideoGenerationService>();
        services.AddScoped<IBackgroundJobs, BackgroundJobs>();

        // Register HttpClient
        services.AddHttpClient();

        // Register Password Hasher & Business Services
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IOAuthService, OAuthService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        services.AddScoped<IVideoAssetService, VideoAssetService>();
        services.AddScoped<IChannelService, ChannelService>();
        services.AddScoped<IContentDnaService, ContentDnaService>();
        services.AddScoped<IWebhookService, WebhookService>();

        return services;
    }
}
