using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using PostFlow.Data;
using PostFlow.Models;

namespace PostFlow.Extensions;

public static class DatabaseExtensions
{
    public static IServiceCollection AddDatabaseConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        return services;
    }

    public static IApplicationBuilder ApplyMigrationsAndSeed(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        // Chạy migration tự động
        dbContext.Database.Migrate();

        // Seed dữ liệu mẫu nếu DB trống
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        DbSeeder.Seed(dbContext, passwordHasher);

        return app;
    }
}
