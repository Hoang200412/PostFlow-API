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

            // 3. Cấu hình bảng VideoAsset
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
            });

            // 4. Cấu hình bảng UploadJob
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
        }
    }
}
