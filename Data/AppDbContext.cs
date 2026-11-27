using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
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
        public DbSet<MediaLibraryAsset> MediaLibraryAssets { get; set; } = null!;
        public DbSet<TrendChannel> TrendChannels { get; set; } = null!;
        public DbSet<TrendVideo> TrendVideos { get; set; } = null!;
        public DbSet<TrendVideoSnapshot> TrendVideoSnapshots { get; set; } = null!;
        public DbSet<TrendCollectionRun> TrendCollectionRuns { get; set; } = null!;
        public DbSet<AdminAuditLog> AdminAuditLogs { get; set; } = null!;
        public DbSet<UserDailyAiUsage> UserDailyAiUsages { get; set; } = null!;
        public DbSet<TtsVoice> TtsVoices { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 1. Cấu hình bảng User
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Username).IsUnique();
                entity.HasIndex(e => e.Email).IsUnique();
                entity.Property(e => e.Plan).HasDefaultValue(UserPlans.Trial);

                // Xóa mềm: user đã xóa bị ẩn khỏi mọi truy vấn thông thường (dùng IgnoreQueryFilters để xem lại)
                entity.HasQueryFilter(e => e.DeletedAt == null);
            });

            modelBuilder.Entity<AdminAuditLog>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Metadata).HasColumnType("jsonb");
                entity.HasIndex(e => e.CreatedAt);
                entity.HasIndex(e => new { e.TargetType, e.TargetId });
                entity.HasIndex(e => e.AdminId);
            });

            modelBuilder.Entity<UserDailyAiUsage>(entity =>
            {
                entity.HasKey(e => new { e.UserId, e.UsageDate });
                entity.Property(e => e.UsageDate).HasColumnType("date");
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // 2. Cấu hình bảng SocialAccount
            modelBuilder.Entity<SocialAccount>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Platform).HasConversion<string>();

                // Đảm bảo không bị trùng lặp tài khoản cùng platform của 1 user
                entity.HasIndex(e => new { e.UserId, e.Platform, e.ChannelIdOrPageId }).IsUnique();

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

            // ValueComparer cho List<string> JSONB properties
            var stringListComparer = new ValueComparer<List<string>>(
                (c1, c2) => c1 == null && c2 == null || (c1 != null && c2 != null && c1.SequenceEqual(c2)),
                c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c.ToList());

            modelBuilder.Entity<MediaLibraryAsset>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Tags)
                    .HasColumnType("jsonb")
                    .HasConversion(
                        v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
                        v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions)null!) ?? new List<string>());
                entity.Property(e => e.Tags).Metadata.SetValueComparer(stringListComparer);
                entity.HasIndex(e => new { e.Type, e.IsActive });
                entity.HasIndex(e => e.CreatedAt);
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
                entity.Property(e => e.Tags).Metadata.SetValueComparer(stringListComparer);

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
                entity.Property(e => e.Type)
                    .HasConversion(v => v.ToString(), v => ParseNotificationType(v));

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
                entity.Property(e => e.Hashtags).Metadata.SetValueComparer(stringListComparer);
                entity.Property(e => e.Keywords)
                    .HasColumnType("jsonb")
                    .HasConversion(
                        value => JsonSerializer.Serialize(value, (JsonSerializerOptions)null!),
                        value => JsonSerializer.Deserialize<List<string>>(value, (JsonSerializerOptions)null!) ?? new List<string>());
                entity.Property(e => e.Keywords).Metadata.SetValueComparer(stringListComparer);
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(e => new { e.UserId, e.CreatedAt });
            });

            // 12. Dữ liệu benchmark xu hướng
            modelBuilder.Entity<TrendChannel>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.Platform, e.ExternalId }).IsUnique();
            });

            modelBuilder.Entity<TrendVideo>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Tags)
                    .HasColumnType("jsonb")
                    .HasConversion(
                        value => JsonSerializer.Serialize(value, (JsonSerializerOptions)null!),
                        value => JsonSerializer.Deserialize<List<string>>(value, (JsonSerializerOptions)null!) ?? new List<string>());
                entity.Property(e => e.Tags).Metadata.SetValueComparer(stringListComparer);

                entity.HasIndex(e => new { e.Platform, e.ExternalId }).IsUnique();
                entity.HasIndex(e => new { e.Platform, e.CategorySlug, e.PublishedAt });
                entity.HasIndex(e => new { e.IsTracking, e.PublishedAt });

                entity.HasOne(e => e.Channel)
                    .WithMany(c => c.Videos)
                    .HasForeignKey(e => e.TrendChannelId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<TrendVideoSnapshot>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.TrendVideoId, e.CapturedAt });

                entity.HasOne(e => e.TrendVideo)
                    .WithMany(v => v.Snapshots)
                    .HasForeignKey(e => e.TrendVideoId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<TrendCollectionRun>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.StartedAt);
            });

            // 13. Cấu hình bảng TtsVoices (Giọng đọc TTS)
            modelBuilder.Entity<TtsVoice>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.VoiceId).IsUnique();
                entity.HasIndex(e => new { e.IsActive, e.SortOrder });
            });
        }

        private static NotificationType ParseNotificationType(string value)
            => Enum.TryParse<NotificationType>(value, true, out var result) ? result : NotificationType.Info;
    }
}
