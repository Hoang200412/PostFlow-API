using Hangfire;
using Serilog;
using Microsoft.AspNetCore.Mvc;
using PostFlow.Extensions;
using PostFlow.Middleware;

// Cấu hình Logger tĩnh Serilog trước khi khởi tạo WebApplication Host
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("Logs/postflow-.txt", 
        rollingInterval: RollingInterval.Day, 
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try {
    Log.Information("Khởi động ứng dụng PostFlow API...");

    var builder = WebApplication.CreateBuilder(args);

    // Kích hoạt Serilog làm logger chính của ứng dụng
    builder.Host.UseSerilog();

    // 1. Đăng ký Controllers & Các dịch vụ mở rộng
    builder.Services.AddControllers()
        .ConfigureApiBehaviorOptions(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var errorMessages = context.ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .Where(m => !string.IsNullOrWhiteSpace(m))
                    .ToList();

                var message = errorMessages.Count > 0
                    ? string.Join(", ", errorMessages)
                    : "Dữ liệu gửi lên không hợp lệ.";

                var responsePayload = new
                {
                    statusCode = StatusCodes.Status400BadRequest,
                    message = message,
                    timestamp = DateTime.UtcNow
                };

                return new BadRequestObjectResult(responsePayload);
            };
        });
    builder.Services.AddCorsPolicy();
    builder.Services.AddDatabaseConfiguration(builder.Configuration);
    builder.Services.AddHangfireConfiguration(builder.Configuration);
    builder.Services.AddApplicationServices(builder.Configuration);
    builder.Services.AddJwtAuthentication(builder.Configuration);
    builder.Services.AddSwaggerDocumentation();

    var app = builder.Build();

    // 2. Middleware xử lý lỗi tập trung
    app.UseMiddleware<GlobalExceptionMiddleware>();

    // 3. Tự động áp dụng Database Migrations và Seed dữ liệu mẫu
    app.ApplyMigrationsAndSeed();

    // 4. Cấu hình Swagger UI (cho môi trường Development)
    app.UseSwaggerDocumentation();

    // 5. Cấu hình Security, CORS & Authentication
    app.UseCors("AllowAll");
    app.UseAuthentication();
    app.UseAuthorization();

    // 6. Hangfire Dashboard & Background Job Scheduler
    app.UseHangfireDashboard();
    app.RegisterRecurringJobs();

    // 7. Route Endpoints
    app.MapControllers();
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Ứng dụng PostFlow API bị dừng bất thường!");
}
finally
{
    Log.CloseAndFlush();
}
