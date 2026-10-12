using Microsoft.AspNetCore.Identity;
using PostFlow.Configuration;
using PostFlow.Models;
using PostFlow.Services;
using PostFlow.Services.Interfaces;
using PostFlow.Services.Publishers;

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

        // Bind Platform API & Storage Settings from appsettings.json
        services.Configure<PlatformSettings>(configuration.GetSection("PlatformSettings"));
        services.Configure<CloudflareR2Settings>(configuration.GetSection("CloudflareR2"));
        services.Configure<AISettings>(configuration.GetSection("AI"));

        // Register Platform-specific publishers
        services.AddTransient<ISocialPublisher, YouTubePublisher>();
        services.AddTransient<ISocialPublisher, TikTokPublisher>();
        services.AddTransient<ISocialPublisher, FacebookReelsPublisher>();

        // Register Factory & Publishing Service
        services.AddTransient<IPlatformPublisherFactory, PlatformPublisherFactory>();
        services.AddScoped<IPublishingService, PublishingService>();

        // Register Cloudflare R2 Storage, Script/Video Generation, and Background Jobs Services
        services.AddScoped<IStorageService, CloudflareR2StorageService>();
        services.AddScoped<IScriptGenerationService, ScriptGenerationService>();
        services.AddScoped<IVideoGenerationService, VideoGenerationService>();
        services.AddScoped<IBackgroundJobs, BackgroundJobs>();

        // Register HttpClient & MemoryCache
        services.AddHttpClient();
        services.AddMemoryCache();

        // Register Password Hasher & Business Services
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IOAuthService, OAuthService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        services.AddScoped<IVideoAssetService, VideoAssetService>();
        services.AddScoped<IVideoCreationSessionService, VideoCreationSessionService>();
        services.AddScoped<IChannelService, ChannelService>();
        services.AddScoped<IContentDnaService, ContentDnaService>();
        services.AddScoped<IWebhookService, WebhookService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<ITrendService, TrendService>();
        services.AddScoped<IPostService, PostService>();
        services.AddScoped<IImageGenerationService, ImageGenerationService>();

        return services;
    }
}
