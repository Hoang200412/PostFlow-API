using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PostFlow.Configuration;
using PostFlow.Data;
using PostFlow.Models;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Services.Interfaces;

namespace PostFlow.Services;

public class MotionVideoBackgroundJobs
{
    private readonly AppDbContext _context;
    private readonly IStorageService _storage;
    private readonly MotionVideoJobManager _jobs;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<MotionVideoBackgroundJobs> _logger;
    private readonly StockVideoSettings _settings;

    public MotionVideoBackgroundJobs(
        AppDbContext context,
        IStorageService storage,
        MotionVideoJobManager jobs,
        IHttpClientFactory httpClientFactory,
        IOptions<StockVideoSettings> settings,
        ILogger<MotionVideoBackgroundJobs> logger)
    {
        _context = context;
        _storage = storage;
        _jobs = jobs;
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task RenderAsync(string jobId, string userId, string requestJson)
    {
        var request = JsonSerializer.Deserialize<RenderMotionVideoRequest>(requestJson);
        if (request == null) { _jobs.Failed(jobId, "Dữ liệu render không hợp lệ."); return; }

        var workDir = Path.Combine(Path.GetTempPath(), "postflow_motion", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);
        var sceneFiles = new List<string>();
        string? musicPath = null;
        try
        {
            _jobs.Processing(jobId);
            for (var index = 0; index < request.Scenes.Count; index++)
            {
                var scene = request.Scenes[index];
                var sceneDir = Path.Combine(workDir, $"scene_{index + 1}");
                Directory.CreateDirectory(sceneDir);
                var sourcePath = Path.Combine(sceneDir, "source.mp4");
                var audioPath = Path.Combine(sceneDir, "voice.mp3");
                var outputPath = Path.Combine(sceneDir, "scene.mp4");
                var subtitlePath = Path.Combine(sceneDir, "captions.srt");

                if (string.IsNullOrWhiteSpace(scene.VideoUrl) && string.IsNullOrWhiteSpace(scene.ObjectKey))
                    throw new InvalidOperationException($"Scene {index + 1} chưa có stock video.");

                if (string.Equals(scene.Provider, "internal", StringComparison.OrdinalIgnoreCase))
                {
                    var asset = await _context.MediaLibraryAssets.AsNoTracking()
                        .SingleOrDefaultAsync(x => x.Id == scene.ObjectKey && x.Type == "video" && x.IsActive);
                    if (asset == null) throw new InvalidOperationException($"Không tìm thấy video nội bộ cho scene {index + 1}.");
                    var tempPath = await _storage.DownloadToTempFileAsync(asset.ObjectKey);
                    try { File.Copy(tempPath, sourcePath, true); } finally { TryDelete(tempPath); }
                }
                else
                {
                    await DownloadExternalVideoAsync(scene.VideoUrl!, sourcePath);
                }
                var narration = string.IsNullOrWhiteSpace(scene.Narration)
                    ? request.Topic
                    : scene.Narration;
                await GenerateTtsAsync(narration, request.Voice, request.Rate, request.Pitch, audioPath, sceneDir);
                if (!string.Equals(request.SubtitleMode, "none", StringComparison.OrdinalIgnoreCase))
                    await File.WriteAllTextAsync(subtitlePath, BuildSubtitle(narration), new UTF8Encoding(false));
                var videoFilter = BuildVideoFilter(request.SubtitleMode, subtitlePath, scene.MotionPreset);
                await RunFfmpegAsync(
                    $"-y -stream_loop -1 -i \"{sourcePath}\" -i \"{audioPath}\" " +
                    $"-vf \"{videoFilter}\" " +
                    "-map 0:v:0 -map 1:a:0 -c:v libx264 -preset veryfast -pix_fmt yuv420p -c:a aac -b:a 192k -shortest " +
                    $"\"{outputPath}\"");
                sceneFiles.Add(outputPath);
                _jobs.Progress(jobId, index + 1, request.Scenes.Count);
            }

            var finalPath = Path.Combine(workDir, "final.mp4");
            var concatList = Path.Combine(workDir, "concat.txt");
            await File.WriteAllLinesAsync(concatList, sceneFiles.Select(x => $"file '{Path.GetFileName(Path.GetDirectoryName(x))}/scene.mp4'"), new UTF8Encoding(false));
            await RunFfmpegAsync($"-y -f concat -safe 0 -i \"{concatList}\" -c copy \"{finalPath}\"", workDir);
            if (!string.IsNullOrWhiteSpace(request.BackgroundMusicObjectKey) &&
                string.Equals(request.AudioMode, "voice-music", StringComparison.OrdinalIgnoreCase))
            {
                musicPath = await _storage.DownloadToTempFileAsync(request.BackgroundMusicObjectKey);
                var mixedPath = Path.Combine(workDir, "mixed.mp4");
                await RunFfmpegAsync(
                    $"-y -i \"{finalPath}\" -stream_loop -1 -i \"{musicPath}\" " +
                    "-filter_complex \"[1:a]volume=0.12[music];[0:a][music]amix=inputs=2:duration=first:dropout_transition=2[aout]\" " +
                    $"-map 0:v:0 -map \"[aout]\" -c:v copy -c:a aac -b:a 192k -shortest \"{mixedPath}\"");
                File.Move(mixedPath, finalPath, true);
            }

            var video = new VideoAsset
            {
                UserId = userId,
                Title = request.Title,
                Description = $"Video động tạo từ stock video: {request.Topic}",
                Status = "Ready",
                DurationSeconds = request.Scenes.Sum(x => x.DurationSeconds)
            };
            await using (var stream = File.OpenRead(finalPath))
            {
                video.LocalFilePath = await _storage.SaveFileAsync(stream, $"videos/{userId}", $"{Guid.NewGuid():N}.mp4", "video/mp4");
            }
            _context.VideoAssets.Add(video);
            await _context.SaveChangesAsync();
            var url = await _storage.GeneratePresignedDownloadUrlAsync(video.LocalFilePath);
            _jobs.Completed(jobId, video.Id, url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi render Motion Video. JobId: {JobId}", jobId);
            _jobs.Failed(jobId, ex.Message);
        }
        finally
        {
            try { if (Directory.Exists(workDir)) Directory.Delete(workDir, true); }
            catch (Exception ex) { _logger.LogWarning(ex, "Không thể dọn thư mục Motion Video: {Dir}", workDir); }
            if (!string.IsNullOrWhiteSpace(musicPath))
            {
                try { if (File.Exists(musicPath)) File.Delete(musicPath); }
                catch (Exception ex) { _logger.LogWarning(ex, "Không thể xóa nhạc nền tạm: {Path}", musicPath); }
            }
        }
    }

    private static string BuildSubtitle(string text)
    {
        var safeText = text.Replace("\r", string.Empty).Replace("\n", " ").Trim();
        return $"1\n00:00:00,000 --> 00:59:59,000\n{safeText}\n";
    }

    private static string BuildVideoFilter(string subtitleMode, string subtitlePath, string motionPreset)
    {
        var filters = new List<string>
        {
            "scale=720:1280:force_original_aspect_ratio=increase",
            "crop=720:1280",
            "fps=30",
            "format=yuv420p"
        };
        if (!string.Equals(subtitleMode, "none", StringComparison.OrdinalIgnoreCase))
        {
            var escapedPath = subtitlePath.Replace("\\", "/").Replace(":", "\\:");
            filters.Add($"subtitles='{escapedPath}':force_style='FontName=Arial,FontSize=18,PrimaryColour=&H00FFFFFF,OutlineColour=&H00000000,Outline=2,Alignment=2,MarginV=80'");
        }
        return string.Join(",", filters);
    }

    private async Task DownloadExternalVideoAsync(string url, string outputPath)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            throw new InvalidOperationException("URL stock video không hợp lệ.");
        if (!uri.Host.Equals("pexels.com", StringComparison.OrdinalIgnoreCase) &&
            !uri.Host.EndsWith(".pexels.com", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Chỉ hỗ trợ stock video từ Pexels.");

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(_settings.DownloadTimeoutSeconds);
        using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        await using var input = await response.Content.ReadAsStreamAsync();
        await using var output = File.Create(outputPath);
        await input.CopyToAsync(output);
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    private static async Task GenerateTtsAsync(string text, string? voice, string? rate, string? pitch, string outputPath, string workDir)
    {
        var textPath = Path.Combine(workDir, $"{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(textPath, text, Encoding.UTF8);
        var args = new StringBuilder($"-m edge_tts --voice \"{voice ?? "vi-VN-NamMinhNeural"}\"");
        if (!string.IsNullOrWhiteSpace(rate)) args.Append($" --rate \"{rate}\"");
        if (!string.IsNullOrWhiteSpace(pitch)) args.Append($" --pitch \"{pitch}\"");
        args.Append($" -f \"{textPath}\" --write-media \"{outputPath}\"");
        var psi = new ProcessStartInfo
        {
            FileName = "python",
            Arguments = args.ToString(),
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Không thể khởi động Edge-TTS.");
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        try { File.Delete(textPath); } catch { }
        if (process.ExitCode != 0 || !File.Exists(outputPath))
            throw new InvalidOperationException($"Không thể tạo voice: {stderr}");
    }

    private static async Task RunFfmpegAsync(string arguments, string? workingDirectory = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = GetFfmpegPath(),
            Arguments = arguments,
            WorkingDirectory = workingDirectory ?? string.Empty,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Không thể khởi động FFmpeg.");
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0) throw new InvalidOperationException($"FFmpeg lỗi: {stderr}");
    }

    private static string GetFfmpegPath()
    {
        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "Tools", "ffmpeg", "ffmpeg.exe"),
            Path.Combine(Directory.GetCurrentDirectory(), "tools", "ffmpeg", "ffmpeg.exe"),
            Path.Combine(AppContext.BaseDirectory, "Tools", "ffmpeg", "ffmpeg.exe"),
            Path.Combine(AppContext.BaseDirectory, "tools", "ffmpeg", "ffmpeg.exe")
        };
        return candidates.FirstOrDefault(File.Exists) ?? "ffmpeg";
    }
}
