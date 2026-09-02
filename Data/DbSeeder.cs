using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Identity;
using PostFlow.Models;

namespace PostFlow.Data
{
    public static class DbSeeder
    {
        public static void Seed(AppDbContext context, IPasswordHasher<User> passwordHasher)
        {
            // 1. Kiểm tra nếu đã có người dùng creator@postflow.com thì không chạy seed
            if (context.Users.Any(u => u.Email == "creator@postflow.com"))
            {
                return;
            }

            // 2. Tạo tài khoản người dùng mẫu
            var creatorUser = new User
            {
                Id = "usr_01",
                Username = "creator",
                Email = "creator@postflow.com",
                Role = "User",
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-30)
            };
            creatorUser.PasswordHash = passwordHasher.HashPassword(creatorUser, "Password123!");

            var adminUser = new User
            {
                Id = "usr_02",
                Username = "admin",
                Email = "admin@postflow.com",
                Role = "Admin",
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-30)
            };
            adminUser.PasswordHash = passwordHasher.HashPassword(adminUser, "Password123!");

            context.Users.AddRange(creatorUser, adminUser);
            context.SaveChanges();

            // 3. Tạo các tài khoản mạng xã hội liên kết (SocialAccounts)
            var youtubeChannel = new SocialAccount
            {
                Id = "acc_01",
                UserId = creatorUser.Id,
                Platform = PlatformType.YouTube,
                AccountName = "Tech Creator VN (Shorts)",
                ChannelIdOrPageId = "UC_techcreator_vn",
                AccessToken = "mock_youtube_access_token",
                RefreshToken = "mock_youtube_refresh_token",
                TokenExpiresAt = DateTime.UtcNow.AddDays(30),
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-20)
            };

            var tiktokChannel = new SocialAccount
            {
                Id = "acc_02",
                UserId = creatorUser.Id,
                Platform = PlatformType.TikTok,
                AccountName = "Tech Creator Official",
                ChannelIdOrPageId = "@techcreator.news",
                AccessToken = "mock_tiktok_access_token",
                RefreshToken = "mock_tiktok_refresh_token",
                TokenExpiresAt = DateTime.UtcNow.AddDays(30),
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-20)
            };

            var facebookChannel = new SocialAccount
            {
                Id = "acc_03",
                UserId = creatorUser.Id,
                Platform = PlatformType.FacebookReels,
                AccountName = "Tech Creator Fanpage (Reels)",
                ChannelIdOrPageId = "fb_techcreator_fanpage",
                AccessToken = "mock_facebook_access_token",
                RefreshToken = "mock_facebook_refresh_token",
                TokenExpiresAt = DateTime.UtcNow.AddDays(30),
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-20)
            };

            context.SocialAccounts.AddRange(youtubeChannel, tiktokChannel, facebookChannel);
            context.SaveChanges();

            // 4. Tạo cấu hình Content DNA (Thương hiệu/Giọng đọc)
            var contentDna1 = new ContentDNA
            {
                Id = "dna_01",
                UserId = creatorUser.Id,
                BrandName = "TechVibe AI",
                VoiceProfile = "vi-VN-NamMinhNeural",
                AvatarAssetPath = "https://images.unsplash.com/photo-1534528741775-53994a69daeb?auto=format&fit=crop&w=200&h=200&q=80",
                ScriptStructure = "{\"hook\":\"Bạn có biết...\",\"body\":\"Chi tiết vấn đề...\",\"cta\":\"Hãy like và đăng ký!\"}",
                CreatedAt = DateTime.UtcNow.AddDays(-15)
            };

            var contentDna2 = new ContentDNA
            {
                Id = "dna_02",
                UserId = creatorUser.Id,
                BrandName = "Daily Motivation",
                VoiceProfile = "vi-VN-HoaiMyNeural",
                AvatarAssetPath = "https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?auto=format&fit=crop&w=200&h=200&q=80",
                ScriptStructure = "{\"hook\":\"Dừng lại 3 giây...\",\"body\":\"Bài học hôm nay...\",\"cta\":\"Chia sẻ nếu thấy ý nghĩa.\"}",
                CreatedAt = DateTime.UtcNow.AddDays(-15)
            };

            context.ContentDNAs.AddRange(contentDna1, contentDna2);
            context.SaveChanges();

            // 5. Tạo các tệp Video (VideoAssets)
            var video1 = new VideoAsset
            {
                Id = "vid_01",
                UserId = creatorUser.Id,
                Title = "Tương lai của AI vào năm 2026",
                Description = "Bạn có biết: Vào năm 2026, 80% video bạn xem trên mạng sẽ do AI tự tạo ra?",
                Tags = new List<string> { "ai", "technology", "future", "xuhuong" },
                LocalFilePath = "uploads/videos/future_ai_2026.mp4",
                DurationSeconds = 50,
                Status = "Ready",
                ContentDNAId = contentDna1.Id,
                CreatedAt = DateTime.UtcNow.AddDays(-10)
            };

            var video2 = new VideoAsset
            {
                Id = "vid_02",
                UserId = creatorUser.Id,
                Title = "Review nhanh iPhone 17 Pro Max",
                Description = "Trên tay chiếc iPhone mới nhất! Đẹp xuất sắc mọi góc nhìn 😍",
                Tags = new List<string> { "iphone17", "reviewcongnghe", "applefan", "hot" },
                LocalFilePath = "uploads/videos/iphone17_review.mp4",
                DurationSeconds = 45,
                Status = "Ready",
                ContentDNAId = contentDna1.Id,
                CreatedAt = DateTime.UtcNow.AddDays(-5)
            };

            var video3 = new VideoAsset
            {
                Id = "vid_03",
                UserId = creatorUser.Id,
                Title = "Mẹo code React 19 cực đỉnh",
                Description = "Làm quen với hook use mới trong React 19!",
                Tags = new List<string> { "reactjs", "react19", "webdev", "coder" },
                LocalFilePath = "uploads/videos/react19_tips.mp4",
                DurationSeconds = 58,
                Status = "Ready",
                ContentDNAId = contentDna1.Id,
                CreatedAt = DateTime.UtcNow.AddDays(-3)
            };

            var video4 = new VideoAsset
            {
                Id = "vid_04",
                UserId = creatorUser.Id,
                Title = "Cách tự động hóa video ngắn",
                Description = "Video đang render tự động...",
                Tags = new List<string> { "automation", "python", "video" },
                LocalFilePath = "",
                DurationSeconds = 60,
                Status = "Rendering",
                ContentDNAId = contentDna1.Id,
                CreatedAt = DateTime.UtcNow.AddMinutes(-30)
            };

            var video5 = new VideoAsset
            {
                Id = "vid_05",
                UserId = creatorUser.Id,
                Title = "5 thói quen của người thành công",
                Description = "Video đang chờ render...",
                Tags = new List<string> { "motivation", "success", "habits" },
                LocalFilePath = "",
                DurationSeconds = 55,
                Status = "Pending",
                ContentDNAId = contentDna2.Id,
                CreatedAt = DateTime.UtcNow.AddMinutes(-5)
            };

            context.VideoAssets.AddRange(video1, video2, video3, video4, video5);
            context.SaveChanges();

            // 6. Tạo lịch trình / tiến trình đăng tải (UploadJobs)
            var job1 = new UploadJob
            {
                Id = "job_01",
                UserId = creatorUser.Id,
                VideoAssetId = video1.Id,
                SocialAccountId = youtubeChannel.Id,
                Platform = PlatformType.YouTube,
                Status = UploadStatus.Completed,
                ProgressPercentage = 100,
                VideoUrl = "https://youtube.com/shorts/mock_yt_1",
                PlatformPostId = "yt_shorts_17283",
                CreatedAt = DateTime.UtcNow.AddDays(-6),
                CompletedAt = DateTime.UtcNow.AddDays(-6).AddMinutes(5)
            };

            var job2 = new UploadJob
            {
                Id = "job_02",
                UserId = creatorUser.Id,
                VideoAssetId = video1.Id,
                SocialAccountId = tiktokChannel.Id,
                Platform = PlatformType.TikTok,
                Status = UploadStatus.Completed,
                ProgressPercentage = 100,
                VideoUrl = "https://tiktok.com/@techcreator/video/mock_tt_1",
                PlatformPostId = "tt_video_8192038",
                CreatedAt = DateTime.UtcNow.AddDays(-6),
                CompletedAt = DateTime.UtcNow.AddDays(-6).AddMinutes(3)
            };

            var job3 = new UploadJob
            {
                Id = "job_03",
                UserId = creatorUser.Id,
                VideoAssetId = video2.Id,
                SocialAccountId = tiktokChannel.Id,
                Platform = PlatformType.TikTok,
                Status = UploadStatus.Completed,
                ProgressPercentage = 100,
                VideoUrl = "https://tiktok.com/@techcreator/video/mock_tt_2",
                PlatformPostId = "tt_video_920381",
                CreatedAt = DateTime.UtcNow.AddDays(-3),
                CompletedAt = DateTime.UtcNow.AddDays(-3).AddMinutes(4)
            };

            var job4 = new UploadJob
            {
                Id = "job_04",
                UserId = creatorUser.Id,
                VideoAssetId = video2.Id,
                SocialAccountId = facebookChannel.Id,
                Platform = PlatformType.FacebookReels,
                Status = UploadStatus.Failed,
                ProgressPercentage = 0,
                ErrorMessage = "Facebook Token expired. Vui lòng kết nối lại tài khoản.",
                ErrorDetails = "OAuthException: The access token has expired or is invalid.",
                CreatedAt = DateTime.UtcNow.AddDays(-3)
            };

            var job5 = new UploadJob
            {
                Id = "job_05",
                UserId = creatorUser.Id,
                VideoAssetId = video3.Id,
                SocialAccountId = youtubeChannel.Id,
                Platform = PlatformType.YouTube,
                Status = UploadStatus.Processing,
                ProgressPercentage = 45,
                CreatedAt = DateTime.UtcNow.AddMinutes(-10)
            };

            var job6 = new UploadJob
            {
                Id = "job_06",
                UserId = creatorUser.Id,
                VideoAssetId = video3.Id,
                SocialAccountId = facebookChannel.Id,
                Platform = PlatformType.FacebookReels,
                Status = UploadStatus.Pending,
                CreatedAt = DateTime.UtcNow.AddMinutes(-10)
            };

            var job7 = new UploadJob
            {
                Id = "job_07",
                UserId = creatorUser.Id,
                VideoAssetId = video1.Id,
                SocialAccountId = facebookChannel.Id,
                Platform = PlatformType.FacebookReels,
                Status = UploadStatus.Pending,
                ScheduledAt = DateTime.UtcNow.AddDays(1),
                PlatformMetadataOverride = "{\"caption\":\"Cập nhật AI mới nhất 2026\",\"tags\":[\"ai\",\"reels\"]}",
                CreatedAt = DateTime.UtcNow.AddHours(-2)
            };

            context.UploadJobs.AddRange(job1, job2, job3, job4, job5, job6, job7);
            context.SaveChanges();

            // 7. Tạo lịch sử thống kê (Analytics) cho các bài đã hoàn thành
            var analyticsList = new List<Analytics>();
            var random = new Random();

            // Job 1 (YouTube) Analytics qua các ngày
            for (int i = 5; i >= 0; i--)
            {
                var capturedDate = DateTime.UtcNow.AddDays(-i);
                analyticsList.Add(new Analytics
                {
                    UploadJobId = job1.Id,
                    Views = 1000 + (5 - i) * 800 + random.Next(-100, 100),
                    Likes = 100 + (5 - i) * 90 + random.Next(-10, 10),
                    Shares = 10 + (5 - i) * 7,
                    Comments = 5 + (5 - i) * 5,
                    Saves = 2 + (5 - i) * 3,
                    CapturedAt = capturedDate
                });
            }

            // Job 2 (TikTok) Analytics qua các ngày
            for (int i = 5; i >= 0; i--)
            {
                var capturedDate = DateTime.UtcNow.AddDays(-i);
                analyticsList.Add(new Analytics
                {
                    UploadJobId = job2.Id,
                    Views = 5000 + (5 - i) * 4500 + random.Next(-500, 500),
                    Likes = 800 + (5 - i) * 600 + random.Next(-50, 50),
                    Shares = 80 + (5 - i) * 100,
                    Comments = 40 + (5 - i) * 50,
                    Saves = 150 + (5 - i) * 130,
                    CapturedAt = capturedDate
                });
            }

            // Job 3 (TikTok Video 2) Analytics qua các ngày
            for (int i = 2; i >= 0; i--)
            {
                var capturedDate = DateTime.UtcNow.AddDays(-i);
                analyticsList.Add(new Analytics
                {
                    UploadJobId = job3.Id,
                    Views = 3000 + (2 - i) * 3500 + random.Next(-300, 300),
                    Likes = 500 + (2 - i) * 550 + random.Next(-40, 40),
                    Shares = 50 + (2 - i) * 75,
                    Comments = 25 + (2 - i) * 45,
                    Saves = 100 + (2 - i) * 90,
                    CapturedAt = capturedDate
                });
            }

            context.Analytics.AddRange(analyticsList);
            context.SaveChanges();
        }
    }
}
