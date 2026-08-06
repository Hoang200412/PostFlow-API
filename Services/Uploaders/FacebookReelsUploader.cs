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
    public class FacebookReelsUploader : IVideoUploader
    {
        private readonly FacebookReelsSettings _settings;
        private readonly ILogger<FacebookReelsUploader> _logger;

        public FacebookReelsUploader(IOptions<PlatformSettings> settings, ILogger<FacebookReelsUploader> logger)
        {
            _settings = settings.Value.FacebookReels;
            _logger = logger;
        }

        public PlatformType Platform => PlatformType.FacebookReels;

        public async Task<UploadResult> UploadAsync(
            string videoFilePath, 
            VideoMetadata metadata, 
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
                /* 
                 * HƯỚNG DẪN TÍCH HỢP FACEBOOK REELS (META GRAPH API) THỰC TẾ:
                 * Meta sử dụng quy trình upload Reels qua 3 bước (Start -> Upload -> Finish):
                 * 
                 * Bước 1: Khởi tạo phiên upload (Initialize Session)
                 *   POST tới endpoint: `https://graph.facebook.com/v19.0/{page_id}/video_reels`
                 *   Query Parameters/Body:
                 *     - upload_phase = "start"
                 *     - access_token = _settings.PageAccessToken
                 *   Kết quả trả về:
                 *     - video_id: ID của video reel trên Facebook.
                 *     - upload_url: URL dùng để đẩy file video lên.
                 * 
                 * Bước 2: Upload file video nhị phân (Upload Video)
                 *   Khởi tạo HTTP client và thực hiện gửi request POST tới `upload_url` nhận được từ Bước 1.
                 *   Headers cần có:
                 *     - Authorization: OAuth _settings.PageAccessToken
                 *     - offset: 0
                 *     - file_size: kích thước file video (bytes)
                 *   Body gửi kèm file nhị phân (hoặc form-data chứa file).
                 *   Tại đây cập nhật progress bằng cách theo dõi số byte đã gửi của HTTP request stream.
                 *   progress?.Report((int)(sentBytes * 100 / totalBytes));
                 * 
                 * Bước 3: Hoàn tất và xuất bản Reels (Finish & Publish)
                 *   POST tới endpoint: `https://graph.facebook.com/v19.0/{page_id}/video_reels`
                 *   Query Parameters/Body:
                 *     - upload_phase = "finish"
                 *     - video_id = video_id (nhận được ở bước 1)
                 *     - video_state = "PUBLISHED"
                 *     - description = metadata.Description // Tiêu đề/mô tả hiển thị trên Reels
                 *     - access_token = _settings.PageAccessToken
                 *   Facebook sẽ tiến hành xử lý ngầm (processing) video và xuất bản công khai.
                 */

                // Giả lập tiến trình upload
                for (int i = 0; i <= 100; i += 30)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    // Giới hạn max progress ở bước upload là 90%, 10% còn lại cho bước finish
                    int currentProgress = Math.Min(i, 90);
                    progress?.Report(currentProgress);
                    _logger.LogInformation("Facebook Reels Uploading: {Percent}%", currentProgress);
                    await Task.Delay(300, cancellationToken); // Giả lập mạng chậm
                }

                // Giả lập bước Finish & Publish
                _logger.LogInformation("Đang hoàn tất (Finishing) phiên upload trên Facebook Reels...");
                await Task.Delay(500, cancellationToken);
                progress?.Report(100);

                string dummyFacebookVideoId = "fb_reel_" + Guid.NewGuid().ToString("N").Substring(0, 15);
                string videoUrl = $"https://www.facebook.com/watch/reel/{dummyFacebookVideoId}";

                _logger.LogInformation("Upload Facebook Reels thành công! Video ID: {VideoId}", dummyFacebookVideoId);

                return UploadResult.Success(dummyFacebookVideoId, videoUrl);
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
