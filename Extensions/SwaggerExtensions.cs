using Microsoft.OpenApi.Models;

namespace PostFlow.Extensions;

public static class SwaggerExtensions
{
    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "PostFlow API",
                Version = "v1",
                Description = "API cho nền tảng tự động hóa sản xuất và phân phối video đa kênh (YouTube, TikTok, Facebook Reels)"
            });

            // Cấu hình nút Authorize JWT Bearer trên giao diện Swagger
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Chỉ cần dán mã JWT Token vào đây (Swagger sẽ tự động thêm 'Bearer ')"
            });

            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        });

        return services;
    }

    public static IApplicationBuilder UseSwaggerDocumentation(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "PostFlow API v1");
                c.RoutePrefix = "swagger"; // Truy cập tại: /swagger
            });
        }
        else
        {
            app.UseHttpsRedirection();
        }

        return app;
    }
}
