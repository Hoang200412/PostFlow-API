using PostFlow.Models.DTOs.Requests;
using PostFlow.Models.DTOs.Responses;
using System.Text.RegularExpressions;
using PostFlow.Services.StockVideo;
using Hangfire;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using PostFlow.Configuration;
using PostFlow.Exceptions;
using PostFlow.Services.Interfaces;

namespace PostFlow.Services;

public class MotionVideoService : IMotionVideoService
{
    private readonly IStockVideoProvider _provider;
    private readonly MotionVideoJobManager _jobManager;
    private readonly Hangfire.IBackgroundJobClient _backgroundJobClient;
    private readonly IStorageService _storage;
    private readonly StockVideoSettings _settings;
    private readonly IMediaLibraryService _mediaLibrary;

    public MotionVideoService(IStockVideoProvider provider, MotionVideoJobManager jobManager, Hangfire.IBackgroundJobClient backgroundJobClient, IStorageService storage, IOptions<StockVideoSettings> settings, IMediaLibraryService mediaLibrary)
    {
        _provider = provider;
        _jobManager = jobManager;
        _backgroundJobClient = backgroundJobClient;
        _storage = storage;
        _settings = settings.Value;
        _mediaLibrary = mediaLibrary;
    }

    public async Task<StockVideoSearchResponse> SearchStockVideosAsync(SearchStockVideoRequest request, CancellationToken cancellationToken = default)
    {
        if (string.Equals(_settings.Provider, "Internal", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(_settings.PexelsApiKey))
            return await _mediaLibrary.SearchVideosAsync(request, cancellationToken);
        try
        {
            var result = await _provider.SearchAsync(request, cancellationToken);
            if (result.Items.Count > 0) return result;
        }
        catch (HttpRequestException) { }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { }
        var fallback = await _mediaLibrary.SearchVideosAsync(request, cancellationToken);
        fallback.FallbackUsed = true;
        return fallback;
    }

    public string CreateRenderJob(string userId, RenderMotionVideoRequest request)
    {
        ValidateRequest(request);
        var jobId = _jobManager.Create(userId, request.Scenes.Count);
        var requestJson = System.Text.Json.JsonSerializer.Serialize(request);
        _backgroundJobClient.Enqueue<MotionVideoBackgroundJobs>(job => job.RenderAsync(jobId, userId, requestJson));
        return jobId;
    }

    public MotionVideoJobStatusDto? GetJob(string jobId, string userId) => _jobManager.Get(jobId, userId);

    private static void ValidateRequest(RenderMotionVideoRequest request)
    {
        var allowedVoices = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "vi-VN-NamMinhNeural", "vi-VN-HoaiMyNeural", "en-US-JennyNeural",
            "en-US-GuyNeural", "en-US-AriaNeural", "en-US-ChristopherNeural"
        };
        if (!string.IsNullOrWhiteSpace(request.Voice) && !allowedVoices.Contains(request.Voice))
            throw new BadRequestException("Giọng đọc không được hỗ trợ.");
        if (!Regex.IsMatch(request.Rate ?? "+0%", @"^[+-]?\d{1,3}%$"))
            throw new BadRequestException("Tốc độ đọc không hợp lệ.");
        if (!Regex.IsMatch(request.Pitch ?? "+0Hz", @"^[+-]?\d{1,3}Hz$"))
            throw new BadRequestException("Cao độ không hợp lệ.");
        if (!new[] { "none", "basic", "highlight" }.Contains(request.SubtitleMode, StringComparer.OrdinalIgnoreCase))
            throw new BadRequestException("Kiểu phụ đề không hợp lệ.");
        if (!new[] { "voice-only", "voice-music" }.Contains(request.AudioMode, StringComparer.OrdinalIgnoreCase))
            throw new BadRequestException("Chế độ âm thanh không hợp lệ.");
        if (request.Scenes.Any(scene => string.IsNullOrWhiteSpace(scene.VideoUrl) && string.IsNullOrWhiteSpace(scene.ObjectKey)))
            throw new BadRequestException("Mỗi scene phải có stock video.");
        if (request.Scenes.Any(scene => string.Equals(scene.Provider, "internal", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(scene.ObjectKey)))
            throw new BadRequestException("Scene nội bộ phải có object key.");
    }

    public async Task<string> UploadMusicAsync(string userId, IFormFile file, CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0) throw new BadRequestException("Vui lòng chọn file nhạc.");
        if (file.Length > _settings.MaxMusicSizeMb * 1024L * 1024L)
            throw new BadRequestException($"File nhạc không được vượt quá {_settings.MaxMusicSizeMb} MB.");
        var allowed = new[] { "audio/mpeg", "audio/wav", "audio/x-wav", "audio/mp4", "audio/aac" };
        if (!allowed.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
            throw new BadRequestException("Chỉ hỗ trợ file MP3, WAV, M4A hoặc AAC.");
        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension)) extension = ".mp3";
        await using var stream = file.OpenReadStream();
        return await _storage.SaveFileAsync(stream, $"audio/motion/{userId}", $"{Guid.NewGuid():N}{extension}", file.ContentType);
    }
}
