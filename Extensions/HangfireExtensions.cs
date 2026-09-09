using Hangfire;
using Hangfire.PostgreSql;
using PostFlow.Services;

namespace PostFlow.Extensions;

public static class HangfireExtensions
{
    public static IServiceCollection AddHangfireConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

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

        return app;
    }
}
