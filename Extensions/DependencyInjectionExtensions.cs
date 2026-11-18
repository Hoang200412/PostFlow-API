using Microsoft.AspNetCore.Identity;
using PostFlow.Configuration;
using PostFlow.Models;
using PostFlow.Services;
using PostFlow.Services.Explainer;
using PostFlow.Services.Interfaces;
using PostFlow.Services.Publishers;
using PostFlow.Services.StockVideo;

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
        services.Configure<StockVideoSettings>(configuration.GetSection("StockVideo"));
        services.Configure<ExplainerVideoSettings>(configuration.GetSection(ExplainerVideoSettings.SectionName));

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
        services.AddSingleton<IScriptJobManager, ScriptJobManager>();
        services.AddSingleton<IVoiceoverJobManager, VoiceoverJobManager>();
        services.AddSingleton<IDubVideoJobManager, DubVideoJobManager>();
        services.AddSingleton<MotionVideoJobManager>();
        services.AddScoped<IMotionVideoService, MotionVideoService>();
        services.AddScoped<IMediaLibraryService, MediaLibraryService>();
        services.AddScoped<MotionVideoBackgroundJobs>();
        services.AddSingleton<ExplainerVideoJobManager>();
        services.AddSingleton<ExplainerToolLocator>();
        services.AddScoped<IGeminiClient, GeminiClient>();
        services.AddScoped<IEdgeTtsAligner, EdgeTtsAligner>();
        services.AddScoped<IRemotionRenderer, RemotionRenderer>();
        services.AddScoped<IExplainerVideoService, ExplainerVideoService>();
        services.AddScoped<ExplainerVideoBackgroundJobs>();
        services.AddHttpClient<PexelsStockVideoProvider>();
        services.AddScoped<IStockVideoProvider, PexelsStockVideoProvider>();

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
