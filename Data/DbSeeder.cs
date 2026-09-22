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
            // 1. Tạo tài khoản người dùng mẫu nếu chưa có
            var creatorUser = context.Users.FirstOrDefault(u => u.Email == "creator@postflow.com");
            var adminUser = context.Users.FirstOrDefault(u => u.Email == "admin@postflow.com");
            var hoangUser = context.Users.FirstOrDefault(u => u.Username == "hoang" || u.Email == "hoang@postflow.com");

            if (creatorUser == null)
            {
                creatorUser = new User
                {
                    Id = "usr_01",
                    Username = "creator",
                    Email = "creator@postflow.com",
                    Role = "User",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-30)
                };
                creatorUser.PasswordHash = passwordHasher.HashPassword(creatorUser, "Password123!");
                context.Users.Add(creatorUser);
            }

            if (adminUser == null)
            {
                adminUser = new User
                {
                    Id = "usr_02",
                    Username = "admin",
                    Email = "admin@postflow.com",
                    Role = "Admin",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-30)
                };
                adminUser.PasswordHash = passwordHasher.HashPassword(adminUser, "Password123!");
                context.Users.Add(adminUser);
            }

            if (hoangUser == null)
            {
                hoangUser = new User
                {
                    Id = "usr_hoang",
                    Username = "hoang",
                    Email = "hoang@postflow.com",
                    Role = "Admin",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                hoangUser.PasswordHash = passwordHasher.HashPassword(hoangUser, "123456");
                context.Users.Add(hoangUser);
            }
            else
            {
                hoangUser.PasswordHash = passwordHasher.HashPassword(hoangUser, "123456");
                hoangUser.Role = "Admin";
                hoangUser.IsActive = true;
            }
            context.SaveChanges();

            // 2. Tạo các tài khoản mạng xã hội liên kết (SocialAccounts) nếu chưa có
            if (!context.SocialAccounts.Any(a => a.UserId == hoangUser.Id))
            {
                var ytHoang = new SocialAccount
                {
                    Id = "acc_hoang_01",
                    UserId = hoangUser.Id,
                    Platform = PlatformType.YouTube,
                    AccountName = "Hoang Tech Shorts",
                    ChannelIdOrPageId = "UC_hoang_tech",
                    AccessToken = "mock_youtube_access_token",
                    RefreshToken = "mock_youtube_refresh_token",
                    TokenExpiresAt = DateTime.UtcNow.AddDays(30),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-20)
                };

                var ttHoang = new SocialAccount
                {
                    Id = "acc_hoang_02",
                    UserId = hoangUser.Id,
                    Platform = PlatformType.TikTok,
                    AccountName = "Hoang AI Official",
                    ChannelIdOrPageId = "@hoang.ai.news",
                    AccessToken = "mock_tiktok_access_token",
                    RefreshToken = "mock_tiktok_refresh_token",
                    TokenExpiresAt = DateTime.UtcNow.AddDays(30),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-20)
                };

                var fbHoang = new SocialAccount
                {
                    Id = "acc_hoang_03",
                    UserId = hoangUser.Id,
                    Platform = PlatformType.FacebookReels,
                    AccountName = "Hoang Creator Fanpage (Reels)",
                    ChannelIdOrPageId = "fb_hoang_fanpage",
                    AccessToken = "mock_facebook_access_token",
                    RefreshToken = "mock_facebook_refresh_token",
                    TokenExpiresAt = DateTime.UtcNow.AddDays(30),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-20)
                };

                context.SocialAccounts.AddRange(ytHoang, ttHoang, fbHoang);
                context.SaveChanges();
            }

            // 3. Tạo cấu hình Content DNA (Thương hiệu/Giọng đọc) nếu chưa có
            if (!context.ContentDNAs.Any(d => d.UserId == hoangUser.Id))
            {
                var contentDnaHoang = new ContentDNA
                {
                    Id = "dna_hoang_01",
                    UserId = hoangUser.Id,
                    BrandName = "Hoang Tech AI",
                    VoiceProfile = "vi-VN-NamMinhNeural",
                    AvatarAssetPath = "https://images.unsplash.com/photo-1534528741775-53994a69daeb?auto=format&fit=crop&w=200&h=200&q=80",
                    ScriptStructure = "{\"hook\":\"Bạn có biết...\",\"body\":\"Chi tiết công nghệ mới...\",\"cta\":\"Follow Hoàng để cập nhật!\"}",
                    CreatedAt = DateTime.UtcNow.AddDays(-15)
                };

                context.ContentDNAs.Add(contentDnaHoang);
                context.SaveChanges();
            }

            // 4. Tạo các tệp Video (VideoAssets) nếu chưa có cho hoangUser
            if (!context.VideoAssets.Any(v => v.UserId == hoangUser.Id))
            {
                var dnaHoang = context.ContentDNAs.FirstOrDefault(d => d.Id == "dna_hoang_01");

                var videoH1 = new VideoAsset
                {
                    Id = "vid_hoang_01",
                    UserId = hoangUser.Id,
                    Title = "Tương lai của AI vào năm 2026",
                    Description = "Bạn có biết: Vào năm 2026, 80% video bạn xem trên mạng sẽ do AI tự tạo ra?",
                    Tags = new List<string> { "ai", "technology", "future", "xuhuong" },
                    LocalFilePath = "https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/ForBiggerBlazes.mp4",
                    DurationSeconds = 50,
                    Status = "Ready",
                    ContentDNAId = dnaHoang?.Id,
                    CreatedAt = DateTime.UtcNow.AddDays(-10),
                    UpdatedAt = DateTime.UtcNow.AddDays(-10)
                };

                var videoH2 = new VideoAsset
                {
                    Id = "vid_hoang_02",
                    UserId = hoangUser.Id,
                    Title = "Review nhanh iPhone 17 Pro Max",
                    Description = "Trên tay chiếc iPhone mới nhất! Đẹp xuất sắc mọi góc nhìn 😍",
                    Tags = new List<string> { "iphone17", "reviewcongnghe", "applefan", "hot" },
                    LocalFilePath = "https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/ForBiggerEscapes.mp4",
                    DurationSeconds = 45,
                    Status = "Ready",
                    ContentDNAId = dnaHoang?.Id,
                    CreatedAt = DateTime.UtcNow.AddDays(-5),
                    UpdatedAt = DateTime.UtcNow.AddDays(-5)
                };

                var videoH3 = new VideoAsset
                {
                    Id = "vid_hoang_03",
                    UserId = hoangUser.Id,
                    Title = "Mẹo code React 19 cực đỉnh",
                    Description = "Làm quen với hook use mới trong React 19 và Tailwind v4!",
                    Tags = new List<string> { "reactjs", "react19", "webdev", "coder" },
                    LocalFilePath = "https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/ForBiggerFun.mp4",
                    DurationSeconds = 58,
                    Status = "Ready",
                    ContentDNAId = dnaHoang?.Id,
                    CreatedAt = DateTime.UtcNow.AddDays(-3),
                    UpdatedAt = DateTime.UtcNow.AddDays(-3)
                };

                var videoH4 = new VideoAsset
                {
                    Id = "vid_hoang_04",
                    UserId = hoangUser.Id,
                    Title = "Cách tự động hóa video ngắn với PostFlow",
                    Description = "Video đang render tự động bởi AI...",
                    Tags = new List<string> { "automation", "postflow", "video" },
                    LocalFilePath = "",
                    DurationSeconds = 60,
                    Status = "Rendering",
                    ContentDNAId = dnaHoang?.Id,
                    CreatedAt = DateTime.UtcNow.AddMinutes(-30),
                    UpdatedAt = DateTime.UtcNow.AddMinutes(-30)
                };

                var videoH5 = new VideoAsset
                {
                    Id = "vid_hoang_05",
                    UserId = hoangUser.Id,
                    Title = "5 thói quen làm chủ công nghệ",
                    Description = "Video đang chờ render...",
                    Tags = new List<string> { "motivation", "success", "habits" },
                    LocalFilePath = "",
                    DurationSeconds = 55,
                    Status = "Pending",
                    ContentDNAId = dnaHoang?.Id,
                    CreatedAt = DateTime.UtcNow.AddMinutes(-5),
                    UpdatedAt = DateTime.UtcNow.AddMinutes(-5)
                };

                context.VideoAssets.AddRange(videoH1, videoH2, videoH3, videoH4, videoH5);
                context.SaveChanges();
            }

            // 5. Tạo lịch trình / tiến trình đăng tải (UploadJobs) nếu chưa có
            if (!context.UploadJobs.Any())
            {
                var vid1 = context.VideoAssets.FirstOrDefault(v => v.Id == "vid_01");
                var vid2 = context.VideoAssets.FirstOrDefault(v => v.Id == "vid_02");
                var vid3 = context.VideoAssets.FirstOrDefault(v => v.Id == "vid_03");

                var ytAcc = context.SocialAccounts.FirstOrDefault(a => a.Platform == PlatformType.YouTube);
                var ttAcc = context.SocialAccounts.FirstOrDefault(a => a.Platform == PlatformType.TikTok);
                var fbAcc = context.SocialAccounts.FirstOrDefault(a => a.Platform == PlatformType.FacebookReels);

                if (vid1 != null && ytAcc != null && ttAcc != null && fbAcc != null)
                {
                    var job1 = new UploadJob
                    {
                        Id = "job_01",
                        UserId = creatorUser.Id,
                        VideoAssetId = vid1.Id,
                        SocialAccountId = ytAcc.Id,
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
                        VideoAssetId = vid1.Id,
                        SocialAccountId = ttAcc.Id,
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
                        VideoAssetId = vid2?.Id ?? vid1.Id,
                        SocialAccountId = ttAcc.Id,
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
                        VideoAssetId = vid2?.Id ?? vid1.Id,
                        SocialAccountId = fbAcc.Id,
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
                        VideoAssetId = vid3?.Id ?? vid1.Id,
                        SocialAccountId = ytAcc.Id,
                        Platform = PlatformType.YouTube,
                        Status = UploadStatus.Processing,
                        ProgressPercentage = 45,
                        CreatedAt = DateTime.UtcNow.AddMinutes(-10)
                    };

                    var job6 = new UploadJob
                    {
                        Id = "job_06",
                        UserId = creatorUser.Id,
                        VideoAssetId = vid3?.Id ?? vid1.Id,
                        SocialAccountId = fbAcc.Id,
                        Platform = PlatformType.FacebookReels,
                        Status = UploadStatus.Pending,
                        CreatedAt = DateTime.UtcNow.AddMinutes(-10)
                    };

                    var job7 = new UploadJob
                    {
                        Id = "job_07",
                        UserId = creatorUser.Id,
                        VideoAssetId = vid1.Id,
                        SocialAccountId = fbAcc.Id,
                        Platform = PlatformType.FacebookReels,
                        Status = UploadStatus.Pending,
                        ScheduledAt = DateTime.UtcNow.AddDays(1),
                        PlatformMetadataOverride = "{\"caption\":\"Cập nhật AI mới nhất 2026\",\"tags\":[\"ai\",\"reels\"]}",
                        CreatedAt = DateTime.UtcNow.AddHours(-2)
                    };

                    context.UploadJobs.AddRange(job1, job2, job3, job4, job5, job6, job7);
                    context.SaveChanges();
                }
            }

            // 6. Tạo lịch sử thống kê (Analytics) cho các bài đã hoàn thành nếu chưa có
            if (!context.Analytics.Any())
            {
                var job1 = context.UploadJobs.FirstOrDefault(j => j.Id == "job_01");
                var job2 = context.UploadJobs.FirstOrDefault(j => j.Id == "job_02");
                var job3 = context.UploadJobs.FirstOrDefault(j => j.Id == "job_03");

                var analyticsList = new List<Analytics>();
                var random = new Random();

                if (job1 != null)
                {
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
                }

                if (job2 != null)
                {
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
                }

                if (job3 != null)
                {
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
                }

                if (analyticsList.Any())
                {
                    context.Analytics.AddRange(analyticsList);
                    context.SaveChanges();
                }
            }
        }
    }
}
