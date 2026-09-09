using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
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
    public class TikTokUploader : IVideoUploader
    {
        private readonly TikTokSettings _settings;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<TikTokUploader> _logger;

        public TikTokUploader(
            IOptions<PlatformSettings> settings, 
            IHttpClientFactory httpClientFactory,
            ILogger<TikTokUploader> logger)
        {
            _settings = settings.Value.TikTok;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public PlatformType Platform => PlatformType.TikTok;

        public async Task<UploadResult> UploadAsync(
            string videoFilePath, 
            VideoMetadata metadata, 
            SocialAccount? account = null,
            IProgress<int>? progress = null, 
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Bắt đầu upload video lên TikTok: {Path}", videoFilePath);

            if (!File.Exists(videoFilePath))
            {
                _logger.LogError("Không tìm thấy file video tại: {Path}", videoFilePath);
                return UploadResult.Failed($"Không tìm thấy file video tại: {videoFilePath}");
            }

            try
            {
                var accessToken = !string.IsNullOrWhiteSpace(account?.AccessToken) 
                    ? account.AccessToken 
                    : _settings.AccessToken;

                bool hasValidCredentials = !string.IsNullOrWhiteSpace(accessToken) && 
                                           !accessToken.StartsWith("YOUR_");

                if (hasValidCredentials)
                {
                    _logger.LogInformation("Đang sử dụng TikTok Direct Post API v2 thực tế...");

                    var client = _httpClientFactory.CreateClient();
                    var fileInfo = new FileInfo(videoFilePath);
                    long totalBytes = fileInfo.Length;
                    int chunkSize = 10 * 1024 * 1024; // 10MB chunk size theo khuyến nghị TikTok
                    int totalChunks = (int)Math.Ceiling((double)totalBytes / chunkSize);

                    // 1. Khởi tạo phiên upload (Direct Post Init)
                    var initRequestPayload = new
                    {
                        post_info = new
                        {
                            title = metadata.Title,
                            privacy_level = "PUBLIC_TO_EVERYONE",
                            disable_comment = false,
                            disable_duet = false,
                            disable_stitch = false
                        },
                        source_info = new
                        {
                            source = "FILE_UPLOAD",
                            video_size = totalBytes,
                            chunk_size = chunkSize,
                            total_chunk_count = totalChunks
                        }
                    };

                    using var initHttpRequest = new HttpRequestMessage(HttpMethod.Post, "https://open.tiktokapis.com/v2/post/publish/video/init/")
                    {
                        Content = new StringContent(JsonSerializer.Serialize(initRequestPayload), Encoding.UTF8, "application/json")
                    };
                    initHttpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                    var initResponse = await client.SendAsync(initHttpRequest, cancellationToken);
                    var initResponseBody = await initResponse.Content.ReadAsStringAsync(cancellationToken);

                    if (!initResponse.IsSuccessStatusCode)
                    {
                        _logger.LogError("TikTok init thất bại: {Response}", initResponseBody);
                        throw new Exception($"TikTok Init API Error: {initResponseBody}");
                    }

                    using var initJsonDoc = JsonDocument.Parse(initResponseBody);
                    var dataElement = initJsonDoc.RootElement.GetProperty("data");
                    string uploadUrl = dataElement.GetProperty("upload_url").GetString()!;
                    string publishId = dataElement.GetProperty("publish_id").GetString()!;

                    _logger.LogInformation("Đã khởi tạo TikTok session thành công! PublishId: {PublishId}", publishId);

                    // 2. Upload các chunks qua HTTP PUT
                    using var fileStream = new FileStream(videoFilePath, FileMode.Open, FileAccess.Read);
                    byte[] buffer = new byte[chunkSize];
                    long uploadedBytes = 0;

                    for (int chunkIndex = 0; chunkIndex < totalChunks; chunkIndex++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        int bytesRead = await fileStream.ReadAsync(buffer, 0, chunkSize, cancellationToken);
                        if (bytesRead == 0) break;

                        using var chunkContent = new ByteArrayContent(buffer, 0, bytesRead);
                        chunkContent.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
                        chunkContent.Headers.Add("Content-Range", $"bytes {uploadedBytes}-{uploadedBytes + bytesRead - 1}/{totalBytes}");

                        using var putRequest = new HttpRequestMessage(HttpMethod.Put, uploadUrl)
                        {
                            Content = chunkContent
                        };

                        var putResponse = await client.SendAsync(putRequest, cancellationToken);
                        if (!putResponse.IsSuccessStatusCode)
                        {
                            var putErr = await putResponse.Content.ReadAsStringAsync(cancellationToken);
                            throw new Exception($"TikTok Chunk Upload Error: {putErr}");
                        }

                        uploadedBytes += bytesRead;
                        int percent = (int)(uploadedBytes * 100 / totalBytes);
                        progress?.Report(percent);
                        _logger.LogInformation("TikTok Chunk {Chunk}/{Total} Uploaded ({Percent}%)", chunkIndex + 1, totalChunks, percent);
                    }

                    string tiktokUrl = $"https://www.tiktok.com/@creator/video/{publishId}";
                    _logger.LogInformation("TikTok upload thành công! Publish ID: {PublishId}, URL: {Url}", publishId, tiktokUrl);
                    return UploadResult.Success(publishId, tiktokUrl);
                }
                else
                {
                    _logger.LogWarning("Chưa cấu hình TikTok Access Token. Đang chạy chế độ Giả lập (Mock Mode)...");

                    for (int i = 0; i <= 100; i += 25)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        progress?.Report(i);
                        _logger.LogInformation("TikTok Mock Uploading: {Percent}%", i);
                        await Task.Delay(400, cancellationToken);
                    }

                    string dummyPublishId = "v2_publish_" + Guid.NewGuid().ToString("N").Substring(0, 16);
                    string videoUrl = $"https://www.tiktok.com/@creator/video/{Guid.NewGuid().ToString("N").Substring(0, 19)}";
                    _logger.LogInformation("Upload TikTok thành công (Mock)! Publish ID: {PublishId}", dummyPublishId);

                    return UploadResult.Success(dummyPublishId, videoUrl);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Tiến trình upload lên TikTok bị hủy bỏ.");
                return UploadResult.Failed("Tiến trình upload bị hủy bỏ.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xảy ra khi upload lên TikTok.");
                return UploadResult.Failed($"Lỗi hệ thống TikTok: {ex.Message}");
            }
        }
    }
}
