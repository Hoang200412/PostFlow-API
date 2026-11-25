using Hangfire;
using Hangfire.PostgreSql;
using PostFlow.Services;
using PostFlow.Services.Interfaces;
using PostFlow.Services.Trends;

namespace PostFlow.Extensions;

public static class HangfireExtensions
{
    public static IServiceCollection AddHangfireConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("SupabaseConnection")
            ?? configuration.GetConnectionString("DefaultConnection");

        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(c => c.UseNpgsqlConnection(connectionString)));

        services.AddHangfireServer();

        return services;
    }

    public static IApplicationBuilder RegisterRecurringJobs(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var recurringJobManager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();

        // Đăng ký Recurring background job cho việc thu thập Analytics (mỗi 6 tiếng)
        recurringJobManager.AddOrUpdate<IBackgroundJobs>(
            "gather-analytics-cron",
            j => j.GatherAnalyticsJobAsync(),
            "0 */6 * * *");

        // Thu thập video xu hướng mới mỗi ngày lúc 01:00 UTC (08:00 giờ Việt Nam)
        recurringJobManager.AddOrUpdate<ITrendCollector>(
            "trend-discover-cron",
            c => c.DiscoverAsync(null),
            "0 1 * * *");

        // Chụp snapshot số liệu cho video đến mốc tuổi 24h/72h/7 ngày, kiểm tra mỗi 3 tiếng
        recurringJobManager.AddOrUpdate<ITrendCollector>(
            "trend-snapshot-cron",
            c => c.SnapshotDueAsync(),
            "0 */3 * * *");

        // Quét token kênh sắp hết hạn / cần kết nối lại mỗi ngày lúc 02:00 UTC (09:00 giờ Việt Nam)
        recurringJobManager.AddOrUpdate<IChannelTokenHealthJob>(
            "channel-token-health-cron",
            j => j.NotifyUnhealthyChannelsAsync(),
            "0 2 * * *");

        return app;
    }
}
