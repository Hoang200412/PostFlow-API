using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Services;
using Google.Apis.Upload;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PostFlow.Configuration;
using PostFlow.Models;
using PostFlow.Services.Interfaces;

namespace PostFlow.Services.Publishers
{
    public class YouTubePublisher : ISocialPublisher
    {
        private readonly YouTubeSettings _settings;
        private readonly ILogger<YouTubePublisher> _logger;

        public YouTubePublisher(IOptions<PlatformSettings> settings, ILogger<YouTubePublisher> logger)
        {
            _settings = settings.Value.YouTube;
            _logger = logger;
        }

        public PlatformType Platform => PlatformType.YouTube;

        public async Task<PublishResult> PublishAsync(
            string videoFilePath, 
            VideoMetadata metadata, 
            SocialAccount? account = null,
            IProgress<int>? progress = null, 
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Bắt đầu xuất bản video lên YouTube: {Path}", videoFilePath);

            if (!File.Exists(videoFilePath))
            {
                _logger.LogError("Không tìm thấy file video tại: {Path}", videoFilePath);
                return PublishResult.Failed($"Không tìm thấy file video tại: {videoFilePath}");
            }

            try
            {
                var clientId = _settings.ClientId;
                var clientSecret = _settings.ClientSecret;
                var refreshToken = account?.RefreshToken ?? _settings.RefreshToken;

                bool hasValidCredentials = !string.IsNullOrWhiteSpace(clientId) && 
                                           !string.IsNullOrWhiteSpace(clientSecret) &&
                                           !string.IsNullOrWhiteSpace(refreshToken) &&

                if (hasValidCredentials)
                {
                    _logger.LogInformation("Đang sử dụng Google API SDK để xuất bản lên YouTube thực tế...");

                    var tokenResponse = new TokenResponse
                    {
                        RefreshToken = refreshToken
                    };

                    var initializer = new GoogleAuthorizationCodeFlow.Initializer
                    {
                        ClientSecrets = new ClientSecrets
                        {
                            ClientId = clientId,
                            ClientSecret = clientSecret
                        },
                        Scopes = new[] { YouTubeService.Scope.YoutubeUpload, YouTubeService.Scope.Youtube }
                    };

                    var flow = new GoogleAuthorizationCodeFlow(initializer);
                    var credential = new UserCredential(flow, account?.UserId ?? "user", tokenResponse);

                    // Refresh token nếu đã hết hạn hoặc stale
                    if (credential.Token.IsStale)
                    {
                        _logger.LogInformation("Làm mới Google OAuth Token cho YouTube...");
                        await credential.RefreshTokenAsync(cancellationToken);
                    }

                    var youtubeService = new YouTubeService(new BaseClientService.Initializer
                    {
                        HttpClientInitializer = credential,
                        ApplicationName = _settings.ApplicationName ?? "PostFlow"
                    });

                    var youtubeVideo = new Video
                    {
                        Snippet = new VideoSnippet
                        {
                            Title = metadata.Title,
                            Description = metadata.Description,
                            Tags = metadata.Tags != null ? metadata.Tags.ToArray() : null,
                            CategoryId = "22" // Mặc định People & Blogs
                        },
                        Status = new VideoStatus
                        {
                            PrivacyStatus = string.IsNullOrWhiteSpace(metadata.PrivacyStatus) ? "public" : metadata.PrivacyStatus.ToLower()
                        }
                    };

                    using var fileStream = new FileStream(videoFilePath, FileMode.Open, FileAccess.Read);
                    var videosInsertRequest = youtubeService.Videos.Insert(youtubeVideo, "snippet,status", fileStream, "video/*");

                    string? publishedVideoId = null;
                    var publishCompletionSource = new TaskCompletionSource<PublishResult>();

                    videosInsertRequest.ProgressChanged += (IUploadProgress uploadProgress) =>
                    {
                        switch (uploadProgress.Status)
                        {
                            case Google.Apis.Upload.UploadStatus.Uploading:
                                if (fileStream.Length > 0)
                                {
                                    var percent = (int)(uploadProgress.BytesSent * 100 / fileStream.Length);
                                    progress?.Report(Math.Min(percent, 99));
                                    _logger.LogDebug("YouTube Publishing: {Percent}% ({BytesSent}/{TotalBytes})", percent, uploadProgress.BytesSent, fileStream.Length);
                                }
                                break;
                            case Google.Apis.Upload.UploadStatus.Completed:
                                progress?.Report(100);
                                _logger.LogInformation("YouTube upload stream hoàn tất!");
                                break;
                            case Google.Apis.Upload.UploadStatus.Failed:
                                _logger.LogError(uploadProgress.Exception, "YouTube upload failed: {Msg}", uploadProgress.Exception?.Message);
                                publishCompletionSource.TrySetResult(PublishResult.Failed($"YouTube SDK Error: {uploadProgress.Exception?.Message}"));
                                break;
                        }
                    };

                    videosInsertRequest.ResponseReceived += (Video video) =>
                    {
                        publishedVideoId = video.Id;
                        string url = $"https://www.youtube.com/watch?v={video.Id}";
                        _logger.LogInformation("YouTube xuất bản thành công! Video ID: {Id}, URL: {Url}", video.Id, url);
                        publishCompletionSource.TrySetResult(PublishResult.Success(video.Id, url));
                    };

                    using (cancellationToken.Register(() => publishCompletionSource.TrySetCanceled()))
                    {
                        var insertTask = videosInsertRequest.UploadAsync(cancellationToken);
                        var completedTask = await Task.WhenAny(insertTask, publishCompletionSource.Task);
                        
                        if (completedTask == publishCompletionSource.Task)
                        {
                            return await publishCompletionSource.Task;
                        }

                        var progressResult = await insertTask;
                        if (progressResult.Status == Google.Apis.Upload.UploadStatus.Failed)
                        {
                            return PublishResult.Failed(progressResult.Exception?.Message ?? "Xuất bản lên YouTube thất bại.");
                        }

                        if (!string.IsNullOrEmpty(publishedVideoId))
                        {
                            return PublishResult.Success(publishedVideoId, $"https://www.youtube.com/watch?v={publishedVideoId}");
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("Chưa cấu hình Google YouTube Credentials hoặc AccessToken. Đang chạy chế độ Giả lập (Mock Mode)...");
                    
                    // Giả lập tiến trình upload
                    for (int i = 0; i <= 100; i += 20)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        progress?.Report(i);
                        _logger.LogInformation("YouTube Mock Publishing: {Percent}%", i);
                        await Task.Delay(400, cancellationToken);
                    }
                }

                string dummyVideoId = Guid.NewGuid().ToString("N").Substring(0, 11);
                string videoUrl = $"https://www.youtube.com/watch?v={dummyVideoId}";
                _logger.LogInformation("Xuất bản YouTube thành công (Mock)! URL: {Url}", videoUrl);

                return PublishResult.Success(dummyVideoId, videoUrl);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Tiến trình xuất bản lên YouTube bị hủy bỏ.");
                return PublishResult.Failed("Tiến trình xuất bản bị hủy bỏ.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xảy ra khi xuất bản lên YouTube.");
                return PublishResult.Failed($"Lỗi hệ thống YouTube: {ex.Message}");
            }
        }
    }
}
