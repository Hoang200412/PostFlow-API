using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PostFlow.Data;
using PostFlow.Models;
using PostFlow.Models.DTOs.Responses;
using PostFlow.Services.Interfaces;

namespace PostFlow.Services;

public class VideoGenerationService : IVideoGenerationService
{
    private readonly AppDbContext _context;
    private readonly IStorageService _storageService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<VideoGenerationService> _logger;

    public VideoGenerationService(
        AppDbContext context,
        IStorageService storageService,
        IHttpClientFactory httpClientFactory,
        ILogger<VideoGenerationService> logger)
    {
        _context = context;
        _storageService = storageService;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Xử lý render và xuất video hoàn chỉnh đẩy lên Cloudflare R2
    /// </summary>
    public async Task<string> GenerateVideoAsync(
        string userId,
        string topic,
        string? contentDnaId,
        string scriptJson,
        string? voice)
    {
        _logger.LogInformation("Bắt đầu quy trình sinh video AI cho User: {UserId}, Chủ đề: {Topic}", userId, topic);

        if (!string.IsNullOrWhiteSpace(contentDnaId))
        {
            var dna = await _context.ContentDNAs.FindAsync(contentDnaId);
            if (dna == null)
            {
                throw new ArgumentException("Không tìm thấy cấu hình Content DNA.");
            }
        }

        // 1. Phân tích kịch bản (Script JSON)
        ScriptDto? script;
        try
        {
            script = JsonSerializer.Deserialize<ScriptDto>(scriptJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi giải mã kịch bản scriptJson");
            throw new ArgumentException("Kịch bản không đúng định dạng JSON.", ex);
        }

        if (script == null || script.Scenes == null || script.Scenes.Count == 0)
        {
            throw new ArgumentException("Kịch bản không có phân cảnh nào để kết xuất video.");
        }

        // 2. Xác định giọng đọc TTS (Mặc định: vi-VN-NamMinhNeural)
        var selectedVoice = string.IsNullOrWhiteSpace(voice) ? "vi-VN-NamMinhNeural" : voice.Trim();

        // 3. Chuẩn bị thư mục làm việc tạm thời
        var workDir = Path.Combine(Path.GetTempPath(), "postflow_render", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);

        var ffmpegPath = GetFFmpegPath();
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.Timeout = TimeSpan.FromSeconds(30);

        var sceneMp4Files = new List<string>();

        try
        {
            // 4. Xử lý kết xuất từng phân cảnh (Scene)
            for (int i = 0; i < script.Scenes.Count; i++)
            {
                var scene = script.Scenes[i];
                var sceneIndex = i + 1;
                _logger.LogInformation("Đang xử lý phân cảnh {Index}/{Total}: {Narration}", sceneIndex, script.Scenes.Count, scene.Narration);

                var audioPath = Path.Combine(workDir, $"scene_{sceneIndex}.mp3");
                var imagePath = Path.Combine(workDir, $"scene_{sceneIndex}.jpg");
                var sceneMp4Path = Path.Combine(workDir, $"scene_{sceneIndex}.mp4");

                // a. Sinh giọng đọc Text-to-Speech (Edge-TTS)
                var narrationText = string.IsNullOrWhiteSpace(scene.Narration)
                    ? (string.IsNullOrWhiteSpace(scene.VisualDescription) ? topic : scene.VisualDescription)
                    : scene.Narration;

                await GenerateTtsAudioAsync(narrationText, selectedVoice, audioPath, workDir);

                // b. Tải ảnh trực quan hoặc tạo ảnh AI
                await DownloadOrGenerateImageAsync(scene, topic, imagePath, httpClient);

                // c. Dùng FFmpeg ghép Ảnh + Audio thành Clip phân cảnh dọc (9:16)
                await RenderSceneVideoAsync(ffmpegPath, imagePath, audioPath, sceneMp4Path);

                sceneMp4Files.Add(sceneMp4Path);
            }

            // 5. Nối tất cả các phân cảnh thành 1 video tổng hợp
            var finalVideoPath = Path.Combine(workDir, "final_output.mp4");
            _logger.LogInformation("Nối {Count} phân cảnh thành video hoàn chỉnh...", sceneMp4Files.Count);

            if (sceneMp4Files.Count == 1)
            {
                File.Copy(sceneMp4Files[0], finalVideoPath, true);
            }
            else
            {
                await ConcatenateScenesAsync(ffmpegPath, sceneMp4Files, finalVideoPath, workDir);
            }

            // 6. Tải video thành phẩm lên Cloudflare R2
            _logger.LogInformation("Đang tải video thành phẩm lên Cloudflare R2...");
            var fileName = $"{Guid.NewGuid():N}.mp4";
            string objectKey;

            using (var fileStream = File.OpenRead(finalVideoPath))
            {
                objectKey = await _storageService.SaveFileAsync(fileStream, "videos", fileName, "video/mp4");
            }

            _logger.LogInformation("Tải lên Cloudflare R2 thành công: {ObjectKey}", objectKey);

            // 7. Ghi nhận nhật ký sử dụng API
            await LogApiUsageAsync(userId, "AI_Video_Render", script.Scenes.Count, 0.0m);

            return objectKey;
        }
        finally
        {
            // Dọn dẹp thư mục tạm để giải phóng ổ cứng
            try
            {
                if (Directory.Exists(workDir))
                {
                    Directory.Delete(workDir, true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể xóa thư mục tạm: {Dir}", workDir);
            }
        }
    }

    /// <summary>
    /// Sinh file âm thanh bằng Microsoft Edge TTS qua Python CLI
    /// </summary>
    private async Task GenerateTtsAudioAsync(string text, string voice, string outputMp3Path, string workDir)
    {
        var textFile = Path.Combine(workDir, $"{Guid.NewGuid():N}_text.txt");
        await File.WriteAllTextAsync(textFile, text, Encoding.UTF8);

        var psi = new ProcessStartInfo
        {
            FileName = "python",
            Arguments = $"-m edge_tts --voice \"{voice}\" -f \"{textFile}\" --write-media \"{outputMp3Path}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null)
        {
            throw new InvalidOperationException("Không thể khởi động tiến trình Python Edge-TTS.");
        }

        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        try { File.Delete(textFile); } catch { }

        if (process.ExitCode != 0 || !File.Exists(outputMp3Path))
        {
            _logger.LogError("Lỗi khi sinh giọng đọc edge-tts: {Stderr}", stderr);
            throw new InvalidOperationException($"Không thể tạo audio từ kịch bản: {stderr}");
        }
    }

    /// <summary>
    /// Tải hình ảnh của phân cảnh, hoặc fallback sang Pollinations AI / Canvas
    /// </summary>
    private async Task DownloadOrGenerateImageAsync(SceneDto scene, string topic, string outputJpgPath, HttpClient httpClient)
    {
        string? candidateUrl = scene.ImageUrl;

        if (string.IsNullOrWhiteSpace(candidateUrl))
        {
            var prompt = !string.IsNullOrWhiteSpace(scene.VisualKeyword)
                ? scene.VisualKeyword
                : (!string.IsNullOrWhiteSpace(scene.VisualDescription) ? scene.VisualDescription : topic);

            var encoded = Uri.EscapeDataString($"{prompt}, vertical 9:16 aspect ratio, cinematic lighting, ultra high quality, 8k");
            candidateUrl = $"https://image.pollinations.ai/prompt/{encoded}?width=720&height=1280&nologo=true";
        }

        try
        {
            var imageBytes = await httpClient.GetByteArrayAsync(candidateUrl);
            await File.WriteAllBytesAsync(outputJpgPath, imageBytes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể tải ảnh từ URL: {Url}. Tạo ảnh canvas nền mặc định.", candidateUrl);
            await GenerateFallbackImageAsync(outputJpgPath);
        }
    }

    /// <summary>
    /// Tạo ảnh canvas nền dự phòng nếu không tải được ảnh ngoại vi
    /// </summary>
    private async Task GenerateFallbackImageAsync(string outputJpgPath)
    {
        var ffmpeg = GetFFmpegPath();
        var psi = new ProcessStartInfo
        {
            FileName = ffmpeg,
            Arguments = $"-y -f lavfi -i color=c=#0f172a:s=720x1280:d=1 -frames:v 1 \"{outputJpgPath}\"",
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = Process.Start(psi);
        if (proc != null)
        {
            await proc.WaitForExitAsync();
        }
    }

    /// <summary>
    /// Ghép ảnh và audio thành video ngắn phân cảnh 9:16 chuẩn H.264 / AAC
    /// </summary>
    private async Task RenderSceneVideoAsync(string ffmpegPath, string imagePath, string audioPath, string outputMp4Path)
    {
        var args = $"-y -loop 1 -i \"{imagePath}\" -i \"{audioPath}\" " +
                   $"-c:v libx264 -tune stillimage -c:a aac -b:a 192k -ar 44100 -pix_fmt yuv420p " +
                   $"-vf \"scale=720:1280:force_original_aspect_ratio=increase,crop=720:1280\" " +
                   $"-shortest \"{outputMp4Path}\"";

        var psi = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = args,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = Process.Start(psi);
        if (proc == null) throw new InvalidOperationException("Không thể khởi động FFmpeg.");

        var stderr = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();

        if (proc.ExitCode != 0 || !File.Exists(outputMp4Path))
        {
            _logger.LogError("Lỗi FFmpeg render scene: {Stderr}", stderr);
            throw new InvalidOperationException($"Lỗi kết xuất phân cảnh video: {stderr}");
        }
    }

    /// <summary>
    /// Nối (Concat) danh sách các phân cảnh thành một file MP4 duy nhất
    /// </summary>
    private async Task ConcatenateScenesAsync(string ffmpegPath, List<string> sceneMp4Files, string outputFinalMp4Path, string workDir)
    {
        var listFile = Path.Combine(workDir, "concat_list.txt");
        var lines = sceneMp4Files.Select(p => $"file '{p.Replace("\\", "/")}'");
        await File.WriteAllLinesAsync(listFile, lines, Encoding.UTF8);

        var args = $"-y -f concat -safe 0 -i \"{listFile}\" -c copy \"{outputFinalMp4Path}\"";

        var psi = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = args,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = Process.Start(psi);
        if (proc == null) throw new InvalidOperationException("Không thể khởi động FFmpeg concat.");

        var stderr = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();

        if (proc.ExitCode != 0 || !File.Exists(outputFinalMp4Path))
        {
            _logger.LogError("Lỗi FFmpeg concat: {Stderr}", stderr);
            throw new InvalidOperationException($"Lỗi nối các phân cảnh video: {stderr}");
        }
    }

    /// <summary>
    /// Định vị file thực thi ffmpeg.exe
    /// </summary>
    private static string GetFFmpegPath()
    {
        var localPath = Path.Combine(Directory.GetCurrentDirectory(), "tools", "ffmpeg", "ffmpeg.exe");
        if (File.Exists(localPath)) return localPath;

        var baseDir = Path.Combine(AppContext.BaseDirectory, "tools", "ffmpeg", "ffmpeg.exe");
        if (File.Exists(baseDir)) return baseDir;

        const string hardcodedDevPath = @"D:\Project\Doan\PostFlow\tools\ffmpeg\ffmpeg.exe";
        if (File.Exists(hardcodedDevPath)) return hardcodedDevPath;

        return "ffmpeg";
    }

    private async Task LogApiUsageAsync(string userId, string provider, int callCount, decimal cost)
    {
        try
        {
            var today = DateTime.Today;
            var log = await _context.ApiUsageLogs
                .FirstOrDefaultAsync(l => l.UserId == userId && l.Provider == provider && l.Date == today);

            if (log == null)
            {
                log = new ApiUsageLog
                {
                    UserId = userId,
                    Provider = provider,
                    CallCount = callCount,
                    CostEstimate = cost,
                    Date = today
                };
                _context.ApiUsageLogs.Add(log);
            }
            else
            {
                log.CallCount += callCount;
                log.CostEstimate += cost;
            }

            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể ghi nhật ký ApiUsageLog");
        }
    }
}
