using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PostFlow.Data;
using PostFlow.Models;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Services.Interfaces;

namespace PostFlow.Services
{
    public interface IBackgroundJobs
    {
        Task RenderVideoJobAsync(
            string videoId,
            string topic,
            string? contentDnaId,
            string scriptJson,
            string? voice,
            string[] channelIds,
            string? scheduledAtIsoStr);
        Task GenerateScriptJobAsync(
            string jobId,
            string userId,
            string requestJson);
        Task GenerateVoiceoverJobAsync(
            string jobId,
            string userId,
            string requestJson);
        Task DubVideoJobAsync(
            string jobId,
            string userId,
            string requestJson);
        Task GatherAnalyticsJobAsync();
    }

    public class BackgroundJobs : IBackgroundJobs
    {
        private readonly AppDbContext _context;
        private readonly IVideoGenerationService _videoGenService;
        private readonly IPublishingService _publishingService;
        private readonly IScriptGenerationService _scriptGenService;
        private readonly IScriptJobManager _scriptJobManager;
        private readonly IVoiceoverJobManager _voiceoverJobManager;
        private readonly IDubVideoJobManager _dubVideoJobManager;
        private readonly INotificationService _notificationService;
        private readonly ILogger<BackgroundJobs> _logger;

        public BackgroundJobs(
            AppDbContext context,
            IVideoGenerationService videoGenService,
            IPublishingService publishingService,
            IScriptGenerationService scriptGenService,
            IScriptJobManager scriptJobManager,
            IVoiceoverJobManager voiceoverJobManager,
            IDubVideoJobManager dubVideoJobManager,
            INotificationService notificationService,
            ILogger<BackgroundJobs> logger)
        {
            _context = context;
            _videoGenService = videoGenService;
            _publishingService = publishingService;
            _scriptGenService = scriptGenService;
            _scriptJobManager = scriptJobManager;
            _voiceoverJobManager = voiceoverJobManager;
            _dubVideoJobManager = dubVideoJobManager;
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task RenderVideoJobAsync(
            string videoId,
            string topic,
            string? contentDnaId,
            string scriptJson,
            string? voice,
            string[] channelIds,
            string? scheduledAtIsoStr)
        {
            var video = await _context.VideoAssets.FindAsync(videoId);
            if (video == null)
            {
                _logger.LogError("Không tìm thấy VideoAsset để render. VideoId: {VideoId}", videoId);
                return;
            }

            try
            {
                _logger.LogInformation("Bắt đầu sinh video tự động bằng AI. VideoId: {VideoId}, Chủ đề: {Topic}", videoId, topic);
                video.Status = "Rendering";
                await _context.SaveChangesAsync();

                var relativePath = await _videoGenService.GenerateVideoAsync(
                    video.UserId,
                    topic,
                    contentDnaId,
                    scriptJson,
                    voice);

                video.LocalFilePath = relativePath;
                video.Status = "Ready";
                await _context.SaveChangesAsync();

                _logger.LogInformation("Sinh video thành công! File: {Path}. Tiến hành tạo Posting Jobs cho các kênh...", relativePath);

                DateTime? scheduledAt = null;
                if (!string.IsNullOrEmpty(scheduledAtIsoStr))
                {
                    if (DateTime.TryParse(scheduledAtIsoStr, out var parsedDate))
                    {
                        scheduledAt = parsedDate.ToUniversalTime();
                    }
                }

                foreach (var channelId in channelIds)
                {
                    try
                    {
                        await _publishingService.EnqueueJobAsync(video.UserId, video.Id, channelId, scheduledAt, null);
                        _logger.LogInformation("Đã tạo PublishingJob cho Kênh {ChannelId}", channelId);
                    }
                    catch (Exception jobEx)
                    {
                        _logger.LogError(jobEx, "Lỗi tạo PublishingJob cho Kênh {ChannelId}", channelId);
                    }
                }
                // Gửi thông báo hoàn tất render video cho người dùng
                try
                {
                    await _notificationService.CreateNotificationAsync(
                        video.UserId,
                        title: "Video đã render hoàn tất!",
                        message: $"Video \"{video.Title}\" đã được xuất bản hoàn tất và sẵn sàng xem hoặc đăng.",
                        type: NotificationType.Success,
                        actionUrl: "/video/create",
                        metadata: new
                        {
                            videoId = video.Id,
                            type = "VIDEO_RENDERED",
                            status = "Ready"
                        }
                    );
                    _logger.LogInformation("Đã gửi thông báo VIDEO_RENDERED cho User: {UserId}", video.UserId);
                }
                catch (Exception notifEx)
                {
                    _logger.LogWarning(notifEx, "Không thể gửi thông báo render video thành công: {Msg}", notifEx.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi trong tiến trình RenderVideoJobAsync cho Video {VideoId}", videoId);
                video.Status = "Failed";
                await _context.SaveChangesAsync();

                try
                {
                    await _notificationService.CreateNotificationAsync(
                        video.UserId,
                        title: "Render video thất bại",
                        message: $"Lỗi khi tạo video: {ex.Message}",
                        type: NotificationType.Error,
                        actionUrl: "/video/create",
                        metadata: new { videoId = video.Id, type = "VIDEO_FAILED", error = ex.Message }
                    );
                }
                catch (Exception notifEx)
                {
                    _logger.LogWarning(notifEx, "Không thể gửi thông báo lỗi render video: {Msg}", notifEx.Message);
                }

                throw;
            }
        }

        public async Task GenerateScriptJobAsync(
            string jobId,
            string userId,
            string requestJson)
        {
            try
            {
                _logger.LogInformation("Bắt đầu thực thi Background Job sinh kịch bản AI. JobId: {JobId}, UserId: {UserId}", jobId, userId);
                _scriptJobManager.SetProcessing(jobId);

                var request = JsonSerializer.Deserialize<GenerateScriptRequest>(requestJson, (JsonSerializerOptions?)null);
                if (request == null)
                {
                    _logger.LogError("Không thể giải mã dữ liệu GenerateScriptRequest cho JobId: {JobId}", jobId);
                    _scriptJobManager.SetFailed(jobId, "Dữ liệu yêu cầu kịch bản không hợp lệ.");
                    return;
                }

                var script = await _scriptGenService.GenerateScriptAsync(userId, request);
                _scriptJobManager.SetCompleted(jobId, script);

                // Tự động lưu kịch bản vào VideoCreationSession cho user
                try
                {
                    var session = await _context.VideoCreationSessions.FirstOrDefaultAsync(s => s.UserId == userId);
                    if (session == null)
                    {
                        session = new VideoCreationSession
                        {
                            UserId = userId,
                            Step = 2,
                            IdeaPrompt = request.CustomPrompt ?? request.Topic,
                            SelectedTone = request.Tone ?? "Năng động, cuốn hút, bắt trend",
                            SelectedDuration = request.TargetDuration ?? "30–45 giây",
                            ScriptJson = JsonSerializer.Serialize(script),
                            UpdatedAt = DateTime.UtcNow,
                        };
                        _context.VideoCreationSessions.Add(session);
                    }
                    else
                    {
                        session.Step = 2;
                        if (!string.IsNullOrWhiteSpace(request.CustomPrompt)) session.IdeaPrompt = request.CustomPrompt;
                        if (!string.IsNullOrWhiteSpace(request.Tone)) session.SelectedTone = request.Tone;
                        if (!string.IsNullOrWhiteSpace(request.TargetDuration)) session.SelectedDuration = request.TargetDuration;
                        session.ScriptJson = JsonSerializer.Serialize(script);
                        session.UpdatedAt = DateTime.UtcNow;
                    }
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Đã đồng bộ kết quả kịch bản AI vào VideoCreationSession cho User: {UserId}", userId);
                }
                catch (Exception sessionEx)
                {
                    _logger.LogWarning(sessionEx, "Không thể tự động đồng bộ VideoCreationSession khi sinh kịch bản (không ảnh hưởng tới job): {Msg}", sessionEx.Message);
                }

                // Gửi thông báo realtime cho người dùng
                try
                {
                    await _notificationService.CreateNotificationAsync(
                        userId,
                        title: "Tạo kịch bản thành công!",
                        message: $"Kịch bản \"{script.Title}\" đã được AI khởi tạo thành công.",
                        type: NotificationType.Success,
                        actionUrl: "/video/create",
                        metadata: new
                        {
                            jobId,
                            type = "SCRIPT_GENERATED",
                            scriptTitle = script.Title,
                            step = 2
                        }
                    );
                    _logger.LogInformation("Đã gửi thông báo SCRIPT_GENERATED thành công cho User: {UserId}", userId);
                }
                catch (Exception notifEx)
                {
                    _logger.LogWarning(notifEx, "Không thể gửi thông báo kịch bản thành công: {Msg}", notifEx.Message);
                }

                _logger.LogInformation("Job sinh kịch bản hoàn tất thành công! JobId: {JobId}, Tiêu đề: {Title}", jobId, script.Title);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi trong tiến trình GenerateScriptJobAsync cho JobId: {JobId}", jobId);
                _scriptJobManager.SetFailed(jobId, ex.Message);

                try
                {
                    await _notificationService.CreateNotificationAsync(
                        userId,
                        title: "Tạo kịch bản thất bại",
                        message: $"Không thể tạo kịch bản: {ex.Message}",
                        type: NotificationType.Error,
                        actionUrl: "/video/create",
                        metadata: new { jobId, type = "SCRIPT_FAILED", error = ex.Message }
                    );
                }
                catch (Exception notifEx)
                {
                    _logger.LogWarning(notifEx, "Không thể gửi thông báo lỗi kịch bản: {Msg}", notifEx.Message);
                }
            }
        }

        public async Task GenerateVoiceoverJobAsync(
            string jobId,
            string userId,
            string requestJson)
        {
            try
            {
                _logger.LogInformation("Bắt đầu thực thi Background Job sinh giọng đọc mẫu. JobId: {JobId}, UserId: {UserId}", jobId, userId);
                _voiceoverJobManager.SetProcessing(jobId);

                var request = JsonSerializer.Deserialize<GenerateVoiceoverRequest>(requestJson, (JsonSerializerOptions?)null);
                if (request == null)
                {
                    _logger.LogError("Không thể giải mã dữ liệu GenerateVoiceoverRequest cho JobId: {JobId}", jobId);
                    _voiceoverJobManager.SetFailed(jobId, "Dữ liệu yêu cầu lồng tiếng không hợp lệ.");
                    return;
                }

                var result = await _videoGenService.GenerateVoiceoverAudioAsync(userId, request);
                _voiceoverJobManager.SetCompleted(jobId, result);

                // Gửi thông báo realtime cho người dùng
                try
                {
                    await _notificationService.CreateNotificationAsync(
                        userId,
                        title: "Sinh giọng đọc mẫu thành công!",
                        message: $"Đoạn audio giọng đọc \"{result.Voice}\" ({result.DurationSeconds:F1}s) đã sẵn sàng để nghe thử.",
                        type: NotificationType.Success,
                        actionUrl: "/voiceover",
                        metadata: new
                        {
                            jobId,
                            type = "VOICEOVER_GENERATED",
                            audioUrl = result.AudioUrl,
                            voice = result.Voice,
                            duration = result.DurationSeconds
                        }
                    );
                    _logger.LogInformation("Đã gửi thông báo VOICEOVER_GENERATED thành công cho User: {UserId}", userId);
                }
                catch (Exception notifEx)
                {
                    _logger.LogWarning(notifEx, "Không thể gửi thông báo giọng đọc thành công: {Msg}", notifEx.Message);
                }

                _logger.LogInformation("Job sinh giọng đọc mẫu hoàn tất thành công! JobId: {JobId}, AudioUrl: {Url}", jobId, result.AudioUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi trong tiến trình GenerateVoiceoverJobAsync cho JobId: {JobId}", jobId);
                _voiceoverJobManager.SetFailed(jobId, ex.Message);

                try
                {
                    await _notificationService.CreateNotificationAsync(
                        userId,
                        title: "Sinh giọng đọc mẫu thất bại",
                        message: $"Không thể tạo giọng đọc: {ex.Message}",
                        type: NotificationType.Error,
                        actionUrl: "/voiceover",
                        metadata: new { jobId, type = "VOICEOVER_FAILED", error = ex.Message }
                    );
                }
                catch (Exception notifEx)
                {
                    _logger.LogWarning(notifEx, "Không thể gửi thông báo lỗi giọng đọc: {Msg}", notifEx.Message);
                }
            }
        }

        public async Task DubVideoJobAsync(string jobId, string userId, string requestJson)
        {
            try
            {
                _logger.LogInformation("Bắt đầu background job lồng tiếng. JobId: {JobId}, UserId: {UserId}", jobId, userId);
                _dubVideoJobManager.SetProcessing(jobId);

                var request = JsonSerializer.Deserialize<DubVideoRequest>(requestJson, (JsonSerializerOptions?)null);
                if (request == null)
                {
                    _dubVideoJobManager.SetFailed(jobId, "Dữ liệu yêu cầu lồng tiếng không hợp lệ.");
                    return;
                }

                var result = await _videoGenService.DubVideoAsync(userId, request);
                _dubVideoJobManager.SetCompleted(jobId, result);
                _logger.LogInformation("Background job lồng tiếng hoàn tất. JobId: {JobId}", jobId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi trong background job lồng tiếng. JobId: {JobId}", jobId);
                _dubVideoJobManager.SetFailed(jobId, ex.Message);
            }
        }

        public async Task GatherAnalyticsJobAsync()
        {
            _logger.LogWarning("Analytics collector chưa được cấu hình; bỏ qua lần thu thập này để tránh ghi dữ liệu giả.");
            await Task.CompletedTask;
        }
    }
}
