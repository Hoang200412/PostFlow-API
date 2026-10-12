using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PostFlow.Models;

namespace PostFlow.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<SocialAccount> SocialAccounts { get; set; } = null!;
        public DbSet<VideoAsset> VideoAssets { get; set; } = null!;
        public DbSet<UploadJob> UploadJobs { get; set; } = null!;
        public DbSet<ContentDNA> ContentDNAs { get; set; } = null!;
        public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;
        public DbSet<ApiUsageLog> ApiUsageLogs { get; set; } = null!;
        public DbSet<Analytics> Analytics { get; set; } = null!;
        public DbSet<SystemSetting> SystemSettings { get; set; } = null!;
        public DbSet<Notification> Notifications { get; set; } = null!;
        public DbSet<VideoCreationSession> VideoCreationSessions { get; set; } = null!;
        public DbSet<Post> Posts { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 1. Cấu hình bảng User
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Username).IsUnique();
                entity.HasIndex(e => e.Email).IsUnique();
            });

            // 2. Cấu hình bảng SocialAccount
            modelBuilder.Entity<SocialAccount>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Platform).HasConversion<string>();

                // Quan hệ User 1 - n SocialAccount
                entity.HasOne(e => e.User)
                    .WithMany(u => u.SocialAccounts)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // 3. Cấu hình bảng ContentDNA
            modelBuilder.Entity<ContentDNA>(entity =>
            {
                entity.HasKey(e => e.Id);

                // Quan hệ User 1 - n ContentDNA
                entity.HasOne(e => e.User)
                    .WithMany(u => u.ContentDNAs)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // 4. Cấu hình bảng VideoAsset
            modelBuilder.Entity<VideoAsset>(entity =>
            {
                entity.HasKey(e => e.Id);

                // Lưu Tags dạng JSONB trong PostgreSQL
                entity.Property(e => e.Tags)
                    .HasColumnType("jsonb")
                    .HasConversion(
                        v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
                        v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions)null!) ?? new List<string>()
                    );

                // Quan hệ User 1 - n VideoAsset
                entity.HasOne(e => e.User)
                    .WithMany(u => u.VideoAssets)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Quan hệ ContentDNA 1 - n VideoAsset
                entity.HasOne(e => e.ContentDNA)
                    .WithMany(c => c.VideoAssets)
                    .HasForeignKey(e => e.ContentDNAId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // 5. Cấu hình bảng UploadJob
            modelBuilder.Entity<UploadJob>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Platform).HasConversion<string>();
                entity.Property(e => e.Status).HasConversion<string>();

                // Quan hệ User 1 - n UploadJob
                entity.HasOne(e => e.User)
                    .WithMany(u => u.UploadJobs)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Quan hệ VideoAsset 1 - n UploadJob
                entity.HasOne(e => e.VideoAsset)
                    .WithMany(v => v.UploadJobs)
                    .HasForeignKey(e => e.VideoAssetId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Quan hệ SocialAccount 1 - n UploadJob
                entity.HasOne(e => e.SocialAccount)
                    .WithMany(sa => sa.UploadJobs)
                    .HasForeignKey(e => e.SocialAccountId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // 6. Cấu hình bảng RefreshToken
            modelBuilder.Entity<RefreshToken>(entity =>
            {
                entity.HasKey(e => e.Id);

                // Quan hệ User 1 - n RefreshToken
                entity.HasOne(e => e.User)
                    .WithMany(u => u.RefreshTokens)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // 7. Cấu hình bảng ApiUsageLog
            modelBuilder.Entity<ApiUsageLog>(entity =>
            {
                entity.HasKey(e => e.Id);

                // Quan hệ User 1 - n ApiUsageLog
                entity.HasOne(e => e.User)
                    .WithMany(u => u.ApiUsageLogs)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // 8. Cấu hình bảng Analytics
            modelBuilder.Entity<Analytics>(entity =>
            {
                entity.HasKey(e => e.Id);

                // Quan hệ UploadJob 1 - n Analytics
                entity.HasOne(e => e.UploadJob)
                    .WithMany(j => j.Analytics)
                    .HasForeignKey(e => e.UploadJobId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // 9. Cấu hình bảng SystemSetting
            modelBuilder.Entity<SystemSetting>(entity =>
            {
                entity.HasKey(e => e.Key);
            });

            // 10. Cấu hình bảng Notification
            modelBuilder.Entity<Notification>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Type).HasConversion<string>();

                entity.HasOne(e => e.User)
                    .WithMany(u => u.Notifications)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => new { e.UserId, e.CreatedAt });
                entity.HasIndex(e => new { e.UserId, e.IsRead });
            });

            // 11. Cấu hình bảng VideoCreationSession
            modelBuilder.Entity<VideoCreationSession>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.UserId).IsUnique();

                entity.HasOne(e => e.User)
                    .WithOne(u => u.VideoCreationSession)
                    .HasForeignKey<VideoCreationSession>(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.CreatedVideo)
                    .WithMany()
                    .HasForeignKey(e => e.CreatedVideoId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<Post>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Hashtags)
                    .HasColumnType("jsonb")
                    .HasConversion(
                        value => JsonSerializer.Serialize(value, (JsonSerializerOptions)null!),
                        value => JsonSerializer.Deserialize<List<string>>(value, (JsonSerializerOptions)null!) ?? new List<string>());
                entity.Property(e => e.Keywords)
                    .HasColumnType("jsonb")
                    .HasConversion(
                        value => JsonSerializer.Serialize(value, (JsonSerializerOptions)null!),
                        value => JsonSerializer.Deserialize<List<string>>(value, (JsonSerializerOptions)null!) ?? new List<string>());
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(e => new { e.UserId, e.CreatedAt });
            });
        }
    }
}
