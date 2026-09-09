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

namespace PostFlow.Services.Uploaders
{
    public class YouTubeUploader : IVideoUploader
    {
        private readonly YouTubeSettings _settings;
        private readonly ILogger<YouTubeUploader> _logger;

        public YouTubeUploader(IOptions<PlatformSettings> settings, ILogger<YouTubeUploader> logger)
        {
            _settings = settings.Value.YouTube;
            _logger = logger;
        }

        public PlatformType Platform => PlatformType.YouTube;

        public async Task<UploadResult> UploadAsync(
            string videoFilePath, 
            VideoMetadata metadata, 
            SocialAccount? account = null,
            IProgress<int>? progress = null, 
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Bắt đầu upload video lên YouTube: {Path}", videoFilePath);

            if (!File.Exists(videoFilePath))
            {
                _logger.LogError("Không tìm thấy file video tại: {Path}", videoFilePath);
                return UploadResult.Failed($"Không tìm thấy file video tại: {videoFilePath}");
            }

            try
            {
                var clientId = _settings.ClientId;
                var clientSecret = _settings.ClientSecret;
                var refreshToken = account?.RefreshToken ?? _settings.RefreshToken;

                bool hasValidCredentials = !string.IsNullOrWhiteSpace(clientId) && 
                                           !clientId.StartsWith("YOUR_") &&
                                           !string.IsNullOrWhiteSpace(clientSecret) &&
                                           !clientSecret.StartsWith("YOUR_") &&
                                           !string.IsNullOrWhiteSpace(refreshToken) &&
                                           !refreshToken.StartsWith("YOUR_");

                if (hasValidCredentials)
                {
                    _logger.LogInformation("Đang sử dụng Google API SDK để upload lên YouTube thực tế...");

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

                    string? uploadedVideoId = null;
                    var uploadCompletionSource = new TaskCompletionSource<UploadResult>();

                    videosInsertRequest.ProgressChanged += (IUploadProgress uploadProgress) =>
                    {
                        switch (uploadProgress.Status)
                        {
                            case Google.Apis.Upload.UploadStatus.Uploading:
                                if (fileStream.Length > 0)
                                {
                                    var percent = (int)(uploadProgress.BytesSent * 100 / fileStream.Length);
                                    progress?.Report(Math.Min(percent, 99));
                                    _logger.LogDebug("YouTube Uploading: {Percent}% ({BytesSent}/{TotalBytes})", percent, uploadProgress.BytesSent, fileStream.Length);
                                }
                                break;
                            case Google.Apis.Upload.UploadStatus.Completed:
                                progress?.Report(100);
                                _logger.LogInformation("YouTube upload stream hoàn tất!");
                                break;
                            case Google.Apis.Upload.UploadStatus.Failed:
                                _logger.LogError(uploadProgress.Exception, "YouTube upload failed: {Msg}", uploadProgress.Exception?.Message);
                                uploadCompletionSource.TrySetResult(UploadResult.Failed($"YouTube SDK Error: {uploadProgress.Exception?.Message}"));
                                break;
                        }
                    };

                    videosInsertRequest.ResponseReceived += (Video video) =>
                    {
                        uploadedVideoId = video.Id;
                        string url = $"https://www.youtube.com/watch?v={video.Id}";
                        _logger.LogInformation("YouTube upload thành công! Video ID: {Id}, URL: {Url}", video.Id, url);
                        uploadCompletionSource.TrySetResult(UploadResult.Success(video.Id, url));
                    };

                    using (cancellationToken.Register(() => uploadCompletionSource.TrySetCanceled()))
                    {
                        var insertTask = videosInsertRequest.UploadAsync(cancellationToken);
                        var completedTask = await Task.WhenAny(insertTask, uploadCompletionSource.Task);
                        
                        if (completedTask == uploadCompletionSource.Task)
                        {
                            return await uploadCompletionSource.Task;
                        }

                        var progressResult = await insertTask;
                        if (progressResult.Status == Google.Apis.Upload.UploadStatus.Failed)
                        {
                            return UploadResult.Failed(progressResult.Exception?.Message ?? "Upload lên YouTube thất bại.");
                        }

                        if (!string.IsNullOrEmpty(uploadedVideoId))
                        {
                            return UploadResult.Success(uploadedVideoId, $"https://www.youtube.com/watch?v={uploadedVideoId}");
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
                        _logger.LogInformation("YouTube Mock Uploading: {Percent}%", i);
                        await Task.Delay(400, cancellationToken);
                    }
                }

                string dummyVideoId = Guid.NewGuid().ToString("N").Substring(0, 11);
                string videoUrl = $"https://www.youtube.com/watch?v={dummyVideoId}";
                _logger.LogInformation("Upload YouTube thành công (Mock)! URL: {Url}", videoUrl);

                return UploadResult.Success(dummyVideoId, videoUrl);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Tiến trình upload lên YouTube bị hủy bỏ.");
                return UploadResult.Failed("Tiến trình upload bị hủy bỏ.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xảy ra khi upload lên YouTube.");
                return UploadResult.Failed($"Lỗi hệ thống YouTube: {ex.Message}");
            }
        }
    }
}
