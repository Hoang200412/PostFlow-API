using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PostFlow.Data
{
    /// <summary>
    /// Factory dùng riêng cho công cụ `dotnet ef`, giúp tạo migration mà không phải khởi động cả ứng dụng
    /// (tránh việc Program.cs tự chạy Migrate/Seed vào database thật khi sinh migration).
    /// </summary>
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            // Phải giống Program.cs để Npgsql ánh xạ DateTime sang "timestamp without time zone" như schema hiện tại
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql("Host=localhost;Database=postflow_design_time;Username=postgres;Password=design")
                .Options;
            return new AppDbContext(options);
        }
    }
}
