using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using PostFlow.Data;
using PostFlow.Models;

using Microsoft.EntityFrameworkCore.Diagnostics;

namespace PostFlow.Extensions;

public static class DatabaseExtensions
{
    public static IServiceCollection AddDatabaseConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("SupabaseConnection")
            ?? configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString)
                   .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));

        return services;
    }

    public static IApplicationBuilder ApplyMigrationsAndSeed(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        // Chạy migration tự động (bỏ qua lỗi nếu đang chạy qua Supabase Pooler và DB đã tồn tại)
        try
        {
            dbContext.Database.Migrate();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DatabaseExtensions] Bỏ qua kiểm tra Migrate khi dùng Pooler: {ex.Message}");
        }

        // Seed dữ liệu mẫu nếu DB trống
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        DbSeeder.Seed(dbContext, passwordHasher);

        return app;
    }
}
