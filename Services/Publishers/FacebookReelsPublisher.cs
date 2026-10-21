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
using PostFlow.Exceptions;
using PostFlow.Models;
using PostFlow.Services.Interfaces;

namespace PostFlow.Services.Publishers
{
    public class FacebookReelsPublisher : ISocialPublisher
    {
        private readonly FacebookReelsSettings _settings;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<FacebookReelsPublisher> _logger;

        public FacebookReelsPublisher(
            IOptions<PlatformSettings> settings, 
            IHttpClientFactory httpClientFactory,
            ILogger<FacebookReelsPublisher> logger)
        {
            _settings = settings.Value.FacebookReels;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public PlatformType Platform => PlatformType.FacebookReels;

        public async Task<PublishResult> PublishAsync(
            string videoFilePath, 
            VideoMetadata metadata, 
            SocialAccount? account = null,
            IProgress<int>? progress = null, 
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Bắt đầu xuất bản video lên Facebook Reels: {Path}", videoFilePath);

            if (!File.Exists(videoFilePath))
            {
                _logger.LogError("Không tìm thấy file video tại: {Path}", videoFilePath);
                throw new NotFoundException($"Không tìm thấy file video tại: {videoFilePath}");
            }

            try
            {
                var pageId = !string.IsNullOrWhiteSpace(account?.ChannelIdOrPageId)
                    ? account.ChannelIdOrPageId
                    : _settings.PageId;

                var pageAccessToken = !string.IsNullOrWhiteSpace(account?.AccessToken)
                    ? account.AccessToken
                    : _settings.PageAccessToken;

                if (string.IsNullOrWhiteSpace(pageId) || string.IsNullOrWhiteSpace(pageAccessToken))
                {
                    _logger.LogError("Chưa cấu hình Facebook Page ID hoặc Page Access Token.");
                    throw new BadRequestException("Chưa cấu hình Facebook Page ID hoặc Page Access Token.");
                }

                _logger.LogInformation("Đang sử dụng Meta Graph API để xuất bản Facebook Reels...");

                var client = _httpClientFactory.CreateClient();
                var fileInfo = new FileInfo(videoFilePath);
                long totalBytes = fileInfo.Length;

                // Bước 1: Khởi tạo phiên upload (Initialize Session)
                string startUrl = $"https://graph.facebook.com/v19.0/{pageId}/video_reels?upload_phase=start&access_token={pageAccessToken}";
                var startResponse = await client.PostAsync(startUrl, null, cancellationToken);
                var startResponseBody = await startResponse.Content.ReadAsStringAsync(cancellationToken);

                if (!startResponse.IsSuccessStatusCode)
                {
                    var errorMsg = ParseMetaErrorMessage(startResponseBody);
                    _logger.LogError("Facebook Reels phase=start thất bại: {Response}", startResponseBody);
                    throw new AppException($"Facebook Reels Start Error: {errorMsg}", startResponse.StatusCode);
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
                        var errorMsg = ParseMetaErrorMessage(uploadErr);
                        _logger.LogError("Facebook Reels phase=upload thất bại: {Response}", uploadErr);
                        throw new AppException($"Facebook Reels Upload Phase Error: {errorMsg}", uploadResponse.StatusCode);
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
                    var errorMsg = ParseMetaErrorMessage(finishResponseBody);
                    _logger.LogError("Facebook Reels phase=finish thất bại: {Response}", finishResponseBody);
                    throw new AppException($"Facebook Reels Finish Error: {errorMsg}", finishResponse.StatusCode);
                }

                progress?.Report(100);
                string facebookVideoUrl = $"https://www.facebook.com/watch/reel/{videoId}";
                _logger.LogInformation("Xuất bản Facebook Reels thành công! Video ID: {VideoId}, URL: {Url}", videoId, facebookVideoUrl);

                return PublishResult.Success(videoId, facebookVideoUrl);
            }
            catch (AppException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xảy ra khi xuất bản lên Facebook Reels.");
                throw new AppException($"Lỗi hệ thống Facebook Reels: {ex.Message}", ex);
            }
        }

        private static string ParseMetaErrorMessage(string responseBody)
        {
            if (string.IsNullOrWhiteSpace(responseBody)) return "Phản hồi rỗng từ Meta API.";
            try
            {
                using var doc = JsonDocument.Parse(responseBody);
                if (doc.RootElement.TryGetProperty("error", out var errorElement))
                {
                    var message = errorElement.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : null;
                    var code = errorElement.TryGetProperty("code", out var codeProp) ? codeProp.GetInt32().ToString() : null;
                    if (!string.IsNullOrEmpty(message))
                    {
                        return code != null ? $"[Code {code}] {message}" : message;
                    }
                }
            }
            catch
            {
                // Fallback nếu không phải JSON
            }
            return responseBody;
        }
    }
}
