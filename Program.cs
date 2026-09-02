using Microsoft.EntityFrameworkCore;
using Hangfire;
using Hangfire.PostgreSql;
using PostFlow.Configuration;
using PostFlow.Data;
using PostFlow.Services;
using PostFlow.Services.Interfaces;
using PostFlow.Services.Uploaders;
using Serilog;
using Scalar.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.Identity;
using PostFlow.Models;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authorization;

// Cấu hình Logger tĩnh Serilog trước khi khởi tạo WebApplication Host
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("Logs/postflow-.txt", 
        rollingInterval: RollingInterval.Day, 
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Khởi động ứng dụng PostFlow API...");

    var builder = WebApplication.CreateBuilder(args);

    // Kích hoạt Serilog làm logger chính của ứng dụng
    builder.Host.UseSerilog();

    // Add services to the container.

    builder.Services.AddControllers();

    // Register CORS policy
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll", policy =>
        {
            policy.WithOrigins("http://localhost:5173", "http://localhost:5174", "http://localhost:3000")
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        });
    });

    // Bind Connection String from appsettings.json
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

    // Register PostgreSQL DbContext
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(connectionString));

    // Register Hangfire with PostgreSQL storage
    builder.Services.AddHangfire(config => config
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UsePostgreSqlStorage(c => c.UseNpgsqlConnection(connectionString)));

    // Add Hangfire Server
    builder.Services.AddHangfireServer();

    // Bind Platform API Settings from appsettings.json
    builder.Services.Configure<PlatformSettings>(builder.Configuration.GetSection("PlatformSettings"));

    // Register Platform-specific uploaders
    builder.Services.AddTransient<IVideoUploader, YouTubeUploader>();
    builder.Services.AddTransient<IVideoUploader, TikTokUploader>();
    builder.Services.AddTransient<IVideoUploader, FacebookReelsUploader>();

    // Register Factory & Orchestration Service
    builder.Services.AddTransient<VideoUploaderFactory>();
    builder.Services.AddScoped<IVideoUploadService, VideoUploadService>(); // Use Scoped service for DB integration

    // Register Storage and Video Generation Services
    builder.Services.AddScoped<IStorageService, StorageService>();
    builder.Services.AddScoped<IVideoGenerationService, VideoGenerationService>();
    builder.Services.AddScoped<IBackgroundJobs, BackgroundJobs>();

    // Register Password Hasher & AuthService
    builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<IAdminService, AdminService>();
    builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
    builder.Services.AddScoped<IAssetService, AssetService>();
    builder.Services.AddScoped<IChannelService, ChannelService>();
    builder.Services.AddScoped<IContentDnaService, ContentDnaService>();
    builder.Services.AddScoped<IPostingJobService, PostingJobService>();
    builder.Services.AddScoped<IVideoService, VideoService>();
    builder.Services.AddScoped<IWebhookService, WebhookService>();

    // Register JWT Authentication
    var jwtSettings = builder.Configuration.GetSection("JwtSettings");
    var key = Encoding.UTF8.GetBytes(jwtSettings["Key"]!);

    builder.Services.AddAuthentication(options =>
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
    });

    // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
    builder.Services.AddOpenApi(options =>
    {
        // 1. Định nghĩa chuẩn bảo mật Bearer (JWT) cho tài liệu OpenAPI
        options.AddDocumentTransformer((document, context, cancellationToken) =>
        {
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes.Add("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Nhập Token JWT của bạn để xác thực API."
            });

            return Task.CompletedTask;
        });

        // 2. Tự động áp dụng khóa bảo mật cho các API có thuộc tính [Authorize]
        options.AddOperationTransformer((operation, context, cancellationToken) =>
        {
            var metadata = context.Description.ActionDescriptor.EndpointMetadata;
            if (metadata.OfType<IAuthorizeData>().Any())
            {
                operation.Security ??= new List<OpenApiSecurityRequirement>();
                operation.Security.Add(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Id = "Bearer",
                            Type = ReferenceType.SecurityScheme
                        }
                    }] = Array.Empty<string>()
                });
            }

            return Task.CompletedTask;
        });
    });

    var app = builder.Build();

    // Kích hoạt Global Exception Handling Middleware làm điểm xử lý lỗi tập trung
    app.UseMiddleware<PostFlow.Middleware.GlobalExceptionMiddleware>();

    // Auto-create/migrate database & tables on startup using Migrations
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbContext.Database.Migrate(); // Apply any pending migrations
        
        // Seed mock data if DB is empty
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        PostFlow.Data.DbSeeder.Seed(dbContext, passwordHasher);
    }

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference(); // Route: /scalar/v1
    }
    else
    {
        app.UseHttpsRedirection();
    }

    app.UseCors("AllowAll");

    app.UseAuthentication();
    app.UseAuthorization();

    // Kích hoạt Hangfire Dashboard (để quản lý / xem các tasks chạy ngầm tại endpoint /hangfire)
    app.UseHangfireDashboard();

    app.MapControllers();

    // Đăng ký Recurring background job cho việc thu thập Analytics (mỗi 6 tiếng)
    using (var scope = app.Services.CreateScope())
    {
        var recurringJobManager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
        recurringJobManager.AddOrUpdate<IBackgroundJobs>(
            "gather-analytics-cron",
            j => j.GatherAnalyticsJobAsync(),
            "0 */6 * * *");
    }

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
