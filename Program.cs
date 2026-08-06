using Microsoft.EntityFrameworkCore;
using Hangfire;
using Hangfire.PostgreSql;
using PostFlow.Configuration;
using PostFlow.Data;
using PostFlow.Services;
using PostFlow.Services.Interfaces;
using PostFlow.Services.Uploaders;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

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

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Auto-create database & tables on startup (để chạy ngay lập tức không cần gõ lệnh Migration)
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

// Kích hoạt Hangfire Dashboard (để quản lý / xem các tasks chạy ngầm tại endpoint /hangfire)
app.UseHangfireDashboard();

app.MapControllers();

app.Run();
