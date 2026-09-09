using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PostFlow.Configuration;
using PostFlow.Models;
using PostFlow.Services.Interfaces;

namespace PostFlow.Services.Uploaders
{
    public class FacebookReelsUploader : IVideoUploader
    {
        private readonly FacebookReelsSettings _settings;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<FacebookReelsUploader> _logger;

        public FacebookReelsUploader(
            IOptions<PlatformSettings> settings, 
            IHttpClientFactory httpClientFactory,
            ILogger<FacebookReelsUploader> logger)
        {
            _settings = settings.Value.FacebookReels;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public PlatformType Platform => PlatformType.FacebookReels;

        public async Task<UploadResult> UploadAsync(
            string videoFilePath, 
            VideoMetadata metadata, 
            SocialAccount? account = null,
            IProgress<int>? progress = null, 
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Bắt đầu upload video lên Facebook Reels: {Path}", videoFilePath);

            if (!File.Exists(videoFilePath))
            {
                _logger.LogError("Không tìm thấy file video tại: {Path}", videoFilePath);
                return UploadResult.Failed($"Không tìm thấy file video tại: {videoFilePath}");
            }

            try
            {
                var pageId = !string.IsNullOrWhiteSpace(account?.ChannelIdOrPageId)
                    ? account.ChannelIdOrPageId
                    : _settings.PageId;

                var pageAccessToken = !string.IsNullOrWhiteSpace(account?.AccessToken)
                    ? account.AccessToken
                    : _settings.PageAccessToken;

                bool hasValidCredentials = !string.IsNullOrWhiteSpace(pageId) &&
                                           !pageId.StartsWith("YOUR_") &&
                                           !string.IsNullOrWhiteSpace(pageAccessToken) &&
                                           !pageAccessToken.StartsWith("YOUR_");

                if (hasValidCredentials)
                {
                    _logger.LogInformation("Đang sử dụng Meta Graph API để upload Facebook Reels thực tế...");

                    var client = _httpClientFactory.CreateClient();
                    var fileInfo = new FileInfo(videoFilePath);
                    long totalBytes = fileInfo.Length;

                    // Bước 1: Khởi tạo phiên upload (Initialize Session)
                    string startUrl = $"https://graph.facebook.com/v19.0/{pageId}/video_reels?upload_phase=start&access_token={pageAccessToken}";
                    var startResponse = await client.PostAsync(startUrl, null, cancellationToken);
                    var startResponseBody = await startResponse.Content.ReadAsStringAsync(cancellationToken);

                    if (!startResponse.IsSuccessStatusCode)
                    {
                        _logger.LogError("Facebook Reels phase=start thất bại: {Response}", startResponseBody);
                        throw new Exception($"Facebook Reels Start Error: {startResponseBody}");
                    }

                    using var startJsonDoc = JsonDocument.Parse(startResponseBody);
                    string videoId = startJsonDoc.RootElement.GetProperty("video_id").GetString()!;
                    string uploadUrl = startJsonDoc.RootElement.GetProperty("upload_url").GetString()!;

                    _logger.LogInformation("Đã khởi tạo Facebook Reels session: VideoId: {VideoId}", videoId);
                    progress?.Report(10);

                    // Bước 2: Upload file video nhị phân (Upload Video)
                    using (var fileStream = new FileStream(videoFilePath, FileMode.Open, FileAccess.Read))
                    {
                        using var streamContent = new StreamContent(fileStream);
                        streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                        using var uploadHttpRequest = new HttpRequestMessage(HttpMethod.Post, uploadUrl)
                        {
                            Content = streamContent
                        };
                        uploadHttpRequest.Headers.Add("Authorization", $"OAuth {pageAccessToken}");
                        uploadHttpRequest.Headers.Add("offset", "0");
                        uploadHttpRequest.Headers.Add("file_size", totalBytes.ToString());

                        var uploadResponse = await client.SendAsync(uploadHttpRequest, cancellationToken);
                        if (!uploadResponse.IsSuccessStatusCode)
                        {
                            var uploadErr = await uploadResponse.Content.ReadAsStringAsync(cancellationToken);
                            throw new Exception($"Facebook Reels Upload Phase Error: {uploadErr}");
                        }
                    }

                    progress?.Report(90);
                    _logger.LogInformation("Đã đẩy file video lên Meta CDN thành công. Tiến hành xuất bản Reels...");

                    // Bước 3: Hoàn tất và xuất bản Reels (Finish & Publish)
                    string desc = Uri.EscapeDataString(metadata.Description ?? metadata.Title ?? "Video ngắn từ PostFlow");
                    string finishUrl = $"https://graph.facebook.com/v19.0/{pageId}/video_reels?upload_phase=finish&video_id={videoId}&video_state=PUBLISHED&description={desc}&access_token={pageAccessToken}";

                    var finishResponse = await client.PostAsync(finishUrl, null, cancellationToken);
                    var finishResponseBody = await finishResponse.Content.ReadAsStringAsync(cancellationToken);

                    if (!finishResponse.IsSuccessStatusCode)
                    {
                        _logger.LogError("Facebook Reels phase=finish thất bại: {Response}", finishResponseBody);
                        throw new Exception($"Facebook Reels Finish Error: {finishResponseBody}");
                    }

                    progress?.Report(100);
                    string facebookVideoUrl = $"https://www.facebook.com/watch/reel/{videoId}";
                    _logger.LogInformation("Upload Facebook Reels thành công! Video ID: {VideoId}, URL: {Url}", videoId, facebookVideoUrl);

                    return UploadResult.Success(videoId, facebookVideoUrl);
                }
                else
                {
                    _logger.LogWarning("Chưa cấu hình Facebook Page ID / Page Access Token. Đang chạy chế độ Giả lập (Mock Mode)...");

                    for (int i = 0; i <= 100; i += 30)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        int currentProgress = Math.Min(i, 90);
                        progress?.Report(currentProgress);
                        _logger.LogInformation("Facebook Reels Mock Uploading: {Percent}%", currentProgress);
                        await Task.Delay(300, cancellationToken);
                    }

                    await Task.Delay(500, cancellationToken);
                    progress?.Report(100);

                    string dummyFacebookVideoId = "fb_reel_" + Guid.NewGuid().ToString("N").Substring(0, 15);
                    string videoUrl = $"https://www.facebook.com/watch/reel/{dummyFacebookVideoId}";
                    _logger.LogInformation("Upload Facebook Reels thành công (Mock)! Video ID: {VideoId}", dummyFacebookVideoId);

                    return UploadResult.Success(dummyFacebookVideoId, videoUrl);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Tiến trình upload lên Facebook Reels bị hủy bỏ.");
                return UploadResult.Failed("Tiến trình upload bị hủy bỏ.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xảy ra khi upload lên Facebook Reels.");
                return UploadResult.Failed($"Lỗi hệ thống Facebook Reels: {ex.Message}");
            }
        }
    }
}
