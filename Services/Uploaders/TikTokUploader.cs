using System;
using System.IO;
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
        private readonly ILogger<TikTokUploader> _logger;

        public TikTokUploader(IOptions<PlatformSettings> settings, ILogger<TikTokUploader> logger)
        {
            _settings = settings.Value.TikTok;
            _logger = logger;
        }

        public PlatformType Platform => PlatformType.TikTok;

        public async Task<UploadResult> UploadAsync(
            string videoFilePath, 
            VideoMetadata metadata, 
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
                /* 
                 * HƯỚNG DẪN TÍCH HỢP TIKTOK API THỰC TẾ:
                 * TikTok cung cấp Share API (Direct Post API). Các bước chính như sau:
                 * 
                 * 1. Khởi tạo phiên Upload (Video Initialization):
                 *    Gửi request POST tới endpoint: `https://open.tiktokapis.com/v2/post/publish/video/init/`
                 *    Kèm theo Access Token trong Authorization Header: Bearer <Access_Token>
                 *    Payload body:
                 *    {
                 *        "post_info": {
                 *            "title": metadata.Title,
                 *            "privacy_level": "PUBLIC_TO_EVERYONE", // Hoặc FRIENDS, SELF_ONLY
                 *            "disable_comment": false,
                 *            "disable_duet": false,
                 *            "disable_stitch": false
                 *        },
                 *        "source_info": {
                 *            "source": "FILE_UPLOAD",
                 *            "video_size": fileBytes.Length,
                 *            "chunk_size": 10485760, // 10MB mỗi chunk
                 *            "total_chunk_count": totalChunks
                 *        }
                 *    }
                 *    
                 * 2. Nhận kết quả trả về từ TikTok:
                 *    Mã phản hồi sẽ chứa `upload_url` và `publish_id`.
                 *    
                 * 3. Upload các chunk video:
                 *    Dùng PUT request để upload các phần nhị phân của video lên `upload_url` vừa nhận được.
                 *    Sử dụng header `Content-Range` để xác định vị trí của chunk hiện tại (ví dụ: bytes 0-10485759/31457280).
                 *    Tại đây bạn có thể cập nhật progress:
                 *    progress?.Report((int)(uploadedBytes * 100 / totalBytes));
                 *    
                 * 4. Check status hoặc đợi webhook của TikTok báo upload thành công.
                 */

                // Giả lập tiến trình upload
                for (int i = 0; i <= 100; i += 25)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report(i);
                    _logger.LogInformation("TikTok Uploading: {Percent}%", i);
                    await Task.Delay(400, cancellationToken); // Giả lập mạng chậm
                }

                string dummyPublishId = "v2_publish_" + Guid.NewGuid().ToString("N").Substring(0, 16);
                string videoUrl = $"https://www.tiktok.com/@creator/video/{Guid.NewGuid().ToString("N").Substring(0, 19)}";

                _logger.LogInformation("Upload TikTok thành công! Publish ID: {PublishId}", dummyPublishId);

                return UploadResult.Success(dummyPublishId, videoUrl);
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
