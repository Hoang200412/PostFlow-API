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
using PostFlow.Exceptions;
using PostFlow.Models;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Models.DTOs.Responses;
using PostFlow.Services.Interfaces;

namespace PostFlow.Services;

public class VideoGenerationService : IVideoGenerationService
{
    private readonly AppDbContext _context;
    private readonly IStorageService _storageService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ITtsService _ttsService;
    private readonly ILogger<VideoGenerationService> _logger;

    public VideoGenerationService(
        AppDbContext context,
        IStorageService storageService,
        IHttpClientFactory httpClientFactory,
        ITtsService ttsService,
        ILogger<VideoGenerationService> logger)
    {
        _context = context;
        _storageService = storageService;
        _httpClientFactory = httpClientFactory;
        _ttsService = ttsService;
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

        // 2. Xác định giọng đọc TTS (Mặc định: diem_trinh)
        var selectedVoice = string.IsNullOrWhiteSpace(voice) ? "diem_trinh" : voice.Trim();

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

                await _ttsService.SynthesizeToFileAsync(narrationText, selectedVoice, null, null, audioPath);

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
        // FFmpeg concat demuxer requires UTF-8 without BOM.
        // Using relative filenames combined with WorkingDirectory avoids Windows path issues.
        var lines = sceneMp4Files.Select(p => $"file '{Path.GetFileName(p)}'");
        var utf8WithoutBom = new UTF8Encoding(false);
        await File.WriteAllLinesAsync(listFile, lines, utf8WithoutBom);

        var args = $"-y -f concat -safe 0 -i \"{listFile}\" -c copy \"{outputFinalMp4Path}\"";

        var psi = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = args,
            WorkingDirectory = workDir,
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
        // 1. Kiểm tra thư mục tools/ffmpeg tại thư mục làm việc hiện tại
        var localPath = Path.Combine(Directory.GetCurrentDirectory(), "Tools", "ffmpeg", "ffmpeg.exe");
        if (File.Exists(localPath)) return localPath;

        var lowerLocalPath = Path.Combine(Directory.GetCurrentDirectory(), "tools", "ffmpeg", "ffmpeg.exe");
        if (File.Exists(lowerLocalPath)) return lowerLocalPath;

        // 2. Kiểm tra thư mục output build
        var baseDir = Path.Combine(AppContext.BaseDirectory, "tools", "ffmpeg", "ffmpeg.exe");
        if (File.Exists(baseDir)) return baseDir;

        var baseToolsDir = Path.Combine(AppContext.BaseDirectory, "Tools", "ffmpeg", "ffmpeg.exe");
        if (File.Exists(baseToolsDir)) return baseToolsDir;

        // 3. Fallback: đường dẫn tuyệt đối dev
        const string hardcodedDevPath = @"D:\Đồ án\PostFlow-API\Tools\ffmpeg\ffmpeg.exe";
        if (File.Exists(hardcodedDevPath)) return hardcodedDevPath;

        // 4. Fallback: sử dụng ffmpeg từ PATH hệ thống
        return "ffmpeg";
    }

    private async Task LogApiUsageAsync(string userId, string provider, int callCount, decimal cost)
    {
        try
        {
            var today = DateTime.UtcNow.Date;
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

    /// <summary>
    /// Sinh file audio giọng đọc mẫu và lưu trữ trên Cloudflare R2
    /// </summary>
    public async Task<VoiceoverPreviewResponse> GenerateVoiceoverAudioAsync(
        string userId,
        GenerateVoiceoverRequest request)
    {
        var workDir = Path.Combine(Path.GetTempPath(), $"postflow_voiceover_{Guid.NewGuid():N}");
        Directory.CreateDirectory(workDir);

        try
        {
            var voice = string.IsNullOrWhiteSpace(request.Voice) ? "diem_trinh" : request.Voice;
            var audioFile = Path.Combine(workDir, $"{Guid.NewGuid():N}.mp3");

            await _ttsService.SynthesizeToFileAsync(request.Text, voice, request.Rate, request.Pitch, audioFile);

            var duration = await GetMediaDurationSecondsAsync(audioFile);

            var now = DateTime.UtcNow;
            var folderName = $"audio/voiceover/{userId}/{now:yyyy}/{now:MM}";
            var fileName = $"{Guid.NewGuid():N}.mp3";

            string objectKey;
            using (var fs = File.OpenRead(audioFile))
            {
                objectKey = await _storageService.SaveFileAsync(fs, folderName, fileName, "audio/mpeg");
            }

            var audioUrl = await _storageService.GeneratePresignedDownloadUrlAsync(objectKey);

            await LogApiUsageAsync(userId, "Voiceover_Preview", 1, 0.0m);

            return new VoiceoverPreviewResponse
            {
                AudioUrl = audioUrl,
                AudioObjectKey = objectKey,
                DurationSeconds = duration,
                Voice = voice,
                Text = request.Text
            };
        }
        finally
        {
            try
            {
                if (Directory.Exists(workDir)) Directory.Delete(workDir, true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể xóa thư mục tạm lồng tiếng: {Dir}", workDir);
            }
        }
    }

    /// <summary>
    /// Thực hiện lồng tiếng vào video (thay thế hoặc hòa trộn với nhạc nền gốc)
    /// </summary>
    public async Task<VideoAssetDto> DubVideoAsync(string userId, DubVideoRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.VideoAssetId))
        {
            throw new BadRequestException("Vui lòng chọn video cần lồng tiếng.");
        }

        var sourceVideo = await _context.VideoAssets
            .FirstOrDefaultAsync(v => v.Id == request.VideoAssetId && v.UserId == userId);

        if (sourceVideo == null)
        {
            throw new NotFoundException($"Không tìm thấy video với ID {request.VideoAssetId}");
        }

        var workDir = Path.Combine(Path.GetTempPath(), $"postflow_dub_{Guid.NewGuid():N}");
        Directory.CreateDirectory(workDir);

        string? tempVideo = null;
        string? tempAudio = null;
        string? tempBgm = null;
        string? tempMusic = null;

        try
        {
            // 1. Tải video gốc về máy chủ
            tempVideo = await _storageService.DownloadToTempFileAsync(sourceVideo.LocalFilePath);

            // 2. Xác định file âm thanh giọng đọc (nếu có yêu cầu lồng tiếng)
            string? audioPath = null;
            var hasVoiceover = !string.IsNullOrWhiteSpace(request.AudioObjectKey) ||
                               !string.IsNullOrWhiteSpace(request.AudioUrl) ||
                               !string.IsNullOrWhiteSpace(request.Text);

            if (!string.IsNullOrWhiteSpace(request.AudioObjectKey))
            {
                tempAudio = await _storageService.DownloadToTempFileAsync(request.AudioObjectKey);
                audioPath = tempAudio;
            }
            else if (!string.IsNullOrWhiteSpace(request.AudioUrl))
            {
                tempAudio = await _storageService.DownloadToTempFileAsync(request.AudioUrl);
                audioPath = tempAudio;
            }
            else if (!string.IsNullOrWhiteSpace(request.Text))
            {
                var voice = string.IsNullOrWhiteSpace(request.Voice) ? "diem_trinh" : request.Voice;
                audioPath = Path.Combine(workDir, $"{Guid.NewGuid():N}.mp3");
                await _ttsService.SynthesizeToFileAsync(request.Text, voice, request.Rate, request.Pitch, audioPath);
            }

            // 3. Tải file nhạc nền (Track 3: BGM) nếu có (hỗ trợ cả BgmObjectKey và BackgroundMusicObjectKey)
            string? bgmPath = null;
            var musicKey = !string.IsNullOrWhiteSpace(request.BgmObjectKey)
                ? request.BgmObjectKey
                : request.BackgroundMusicObjectKey;

            if (!string.IsNullOrWhiteSpace(musicKey))
            {
                tempBgm = await _storageService.DownloadToTempFileAsync(musicKey);
                bgmPath = tempBgm;
            }
            else if (!string.IsNullOrWhiteSpace(request.BgmUrl))
            {
                if (request.BgmUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    request.BgmUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    tempBgm = await _storageService.DownloadToTempFileAsync(request.BgmUrl);
                    bgmPath = tempBgm;
                }
                else if (File.Exists(request.BgmUrl))
                {
                    bgmPath = request.BgmUrl;
                }
            }

            var hasBgm = !string.IsNullOrWhiteSpace(bgmPath);

            // Kiểm tra: Phải có ít nhất 1 nguồn âm thanh bổ sung (Lồng tiếng hoặc Nhạc nền)
            if (!hasVoiceover && !hasBgm)
            {
                throw new BadRequestException("Vui lòng chọn giọng đọc hoặc nhạc nền để xử lý âm thanh cho video.");
            }

            // 4. Kiểm tra xem video gốc có luồng audio không
            var hasAudio = await HasAudioStreamAsync(tempVideo);
            var ffmpeg = GetFFmpegPath();
            var outputMp4 = Path.Combine(workDir, $"{Guid.NewGuid():N}_dubbed.mp4");

            // 5. Xây dựng tham số đầu vào và bộ lọc hòa âm đa luồng động (Dynamic Multi-Track Filter)
            var isMix = string.Equals(request.AudioMode, "mix", StringComparison.OrdinalIgnoreCase);
            var origVol = isMix ? Math.Clamp(request.OriginalAudioVolume, 0.0, 2.0) : 0.0;
            var voiceVol = Math.Clamp(request.VoiceVolume > 0 ? request.VoiceVolume : 1.0, 0.0, 3.0);
            var voiceDelayMs = (int)Math.Max(0, Math.Round(request.StartTimeSeconds * 1000));

            var inputArgs = new List<string> { $"-y -i \"{tempVideo}\"" }; // Input 0: Video
            int nextInputIndex = 1;
            int? voiceInputIndex = null;
            int? bgmInputIndex = null;

            if (hasVoiceover && !string.IsNullOrEmpty(audioPath))
            {
                inputArgs.Add($"-i \"{audioPath}\"");
                voiceInputIndex = nextInputIndex++;
            }

            if (hasBgm && !string.IsNullOrEmpty(bgmPath))
            {
                inputArgs.Add($"-stream_loop -1 -i \"{bgmPath}\"");
                bgmInputIndex = nextInputIndex++;
            }

            var filterChains = new List<string>();
            var mixInputs = new List<string>();

            // Luồng Track 1: Tiếng gốc video (nếu có và âm lượng > 0)
            if (hasAudio && origVol > 0.001)
            {
                var origVolStr = origVol.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                filterChains.Add($"[0:a]volume={origVolStr}[a_orig]");
                mixInputs.Add("[a_orig]");
            }

            // Luồng Track 2: Giọng đọc thuyết minh (Voiceover) nếu được chọn
            if (voiceInputIndex.HasValue)
            {
                var voiceVolStr = voiceVol.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                var vIdx = voiceInputIndex.Value;
                if (voiceDelayMs > 0)
                {
                    filterChains.Add($"[{vIdx}:a]adelay={voiceDelayMs}|{voiceDelayMs},volume={voiceVolStr}[a_voice]");
                }
                else
                {
                    filterChains.Add($"[{vIdx}:a]volume={voiceVolStr}[a_voice]");
                }
                mixInputs.Add("[a_voice]");
            }

            // Luồng Track 3: Nhạc nền BGM nếu được chọn
            if (bgmInputIndex.HasValue)
            {
                var bgmVol = Math.Clamp(request.BgmVolume >= 0 ? request.BgmVolume : 0.2, 0.0, 2.0);
                var bgmVolStr = bgmVol.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                var bgmDelayMs = (int)Math.Max(0, Math.Round(request.BgmStartTimeSeconds * 1000));
                var bIdx = bgmInputIndex.Value;

                if (bgmVol > 0.001)
                {
                    if (bgmDelayMs > 0)
                    {
                        filterChains.Add($"[{bIdx}:a]adelay={bgmDelayMs}|{bgmDelayMs},volume={bgmVolStr}[a_bgm]");
                    }
                    else
                    {
                        filterChains.Add($"[{bIdx}:a]volume={bgmVolStr}[a_bgm]");
                    }
                    mixInputs.Add("[a_bgm]");
                }
            }

            // Hòa âm các luồng
            if (mixInputs.Count > 1)
            {
                filterChains.Add($"{string.Join("", mixInputs)}amix=inputs={mixInputs.Count}:duration=longest[amix]");
                filterChains.Add("[amix]apad[aout]");
            }
            else if (mixInputs.Count == 1)
            {
                filterChains.Add($"{mixInputs[0]}apad[aout]");
            }
            else
            {
                // Dự phòng nếu tất cả âm lượng = 0
                filterChains.Add("aevalsrc=0:d=1[aout]");
            }

            var filterComplex = string.Join(";", filterChains);
            var joinedInputs = string.Join(" ", inputArgs);
            var args = $"{joinedInputs} -filter_complex \"{filterComplex}\" " +
                       $"-map 0:v:0 -map \"[aout]\" -c:v copy -c:a aac -b:a 192k -shortest \"{outputMp4}\"";

            var psi = new ProcessStartInfo
            {
                FileName = ffmpeg,
                Arguments = args,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc == null) throw new InvalidOperationException("Không thể khởi động FFmpeg lồng tiếng.");

            var stderr = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();

            if (proc.ExitCode != 0 || !File.Exists(outputMp4))
            {
                _logger.LogError("Lỗi FFmpeg lồng tiếng: {Stderr}", stderr);
                throw new InvalidOperationException($"Lỗi lồng tiếng video: {stderr}");
            }

            // 5. Tính thời lượng video thành phẩm
            var dubbedDuration = await GetMediaDurationSecondsAsync(outputMp4);

            // 6. Tải video lên Cloudflare R2
            var now = DateTime.UtcNow;
            var folderName = $"videos/{userId}/{now:yyyy}/{now:MM}";
            var fileName = $"{Guid.NewGuid():N}.mp4";

            string objectKey;
            using (var fs = File.OpenRead(outputMp4))
            {
                objectKey = await _storageService.SaveFileAsync(fs, folderName, fileName, "video/mp4");
            }

            // 7. Tạo VideoAsset mới trong DB
            string defaultSuffix;
            if (hasVoiceover && hasBgm)
                defaultSuffix = "(Lồng tiếng & Nhạc nền)";
            else if (hasVoiceover)
                defaultSuffix = "(Lồng tiếng)";
            else
                defaultSuffix = "(Chèn nhạc nền)";

            var title = !string.IsNullOrWhiteSpace(request.Title) 
                ? request.Title 
                : $"{sourceVideo.Title} {defaultSuffix}";

            var description = !string.IsNullOrWhiteSpace(request.Text)
                ? $"Lồng tiếng: {request.Text}"
                : (hasBgm ? $"Video đã chèn nhạc nền từ {sourceVideo.Title}" : sourceVideo.Description);

            var tags = new List<string>(sourceVideo.Tags ?? new List<string>());
            if (hasVoiceover && !tags.Contains("voiceover")) tags.Add("voiceover");
            if (hasBgm && !tags.Contains("bgm")) tags.Add("bgm");
            if (!tags.Contains("dubbed")) tags.Add("dubbed");

            var dubbedAsset = new VideoAsset
            {
                UserId = userId,
                Title = title,
                Description = description,
                Tags = tags,
                LocalFilePath = objectKey,
                DurationSeconds = (int)Math.Round(dubbedDuration > 0 ? dubbedDuration : sourceVideo.DurationSeconds),
                Status = "Ready",
                ContentDNAId = sourceVideo.ContentDNAId,
                CreatedAt = DateTime.UtcNow
            };

            _context.VideoAssets.Add(dubbedAsset);
            await _context.SaveChangesAsync();

            await LogApiUsageAsync(userId, "Voiceover_Dub", 1, 0.0m);

            return dubbedAsset.ToDto();
        }
        finally
        {
            try
            {
                if (tempVideo != null && File.Exists(tempVideo)) File.Delete(tempVideo);
                if (tempAudio != null && File.Exists(tempAudio)) File.Delete(tempAudio);
                if (tempBgm != null && File.Exists(tempBgm)) File.Delete(tempBgm);
                if (tempMusic != null && File.Exists(tempMusic)) File.Delete(tempMusic);
                if (Directory.Exists(workDir)) Directory.Delete(workDir, true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể dọn dẹp thư mục tạm dubbing: {Dir}", workDir);
            }
        }
    }

    private async Task GenerateTtsAudioWithParamsAsync(string text, string voice, string? rate, string? pitch, string outputMp3Path, string workDir)
    {
        var textFile = Path.Combine(workDir, $"{Guid.NewGuid():N}_text.txt");
        await File.WriteAllTextAsync(textFile, text, Encoding.UTF8);

        var argsBuilder = new StringBuilder($"-m edge_tts --voice \"{voice}\"");

        if (!string.IsNullOrWhiteSpace(rate) && rate != "+0%" && rate != "0%")
        {
            var cleanRate = rate.StartsWith("+") || rate.StartsWith("-") ? rate : $"+{rate}";
            argsBuilder.Append($" --rate \"{cleanRate}\"");
        }

        if (!string.IsNullOrWhiteSpace(pitch) && pitch != "+0Hz" && pitch != "0Hz")
        {
            var cleanPitch = pitch.StartsWith("+") || pitch.StartsWith("-") ? pitch : $"+{pitch}";
            argsBuilder.Append($" --pitch \"{cleanPitch}\"");
        }

        argsBuilder.Append($" -f \"{textFile}\" --write-media \"{outputMp3Path}\"");

        var psi = new ProcessStartInfo
        {
            FileName = "python",
            Arguments = argsBuilder.ToString(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null) throw new InvalidOperationException("Không thể khởi động tiến trình Python Edge-TTS.");

        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        try { File.Delete(textFile); } catch { }

        if (process.ExitCode != 0 || !File.Exists(outputMp3Path))
        {
            _logger.LogError("Lỗi khi sinh giọng đọc edge-tts: {Stderr}", stderr);
            throw new InvalidOperationException($"Không thể tạo audio từ văn bản");
        }
    }

    private static string GetFFprobePath()
    {
        var ffmpegPath = GetFFmpegPath();
        if (ffmpegPath.EndsWith("ffmpeg.exe", StringComparison.OrdinalIgnoreCase))
        {
            var probe = ffmpegPath.Substring(0, ffmpegPath.Length - "ffmpeg.exe".Length) + "ffprobe.exe";
            if (File.Exists(probe)) return probe;
        }
        return "ffprobe";
    }

    private async Task<bool> HasAudioStreamAsync(string filePath)
    {
        try
        {
            var ffprobe = GetFFprobePath();
            var psi = new ProcessStartInfo
            {
                FileName = ffprobe,
                Arguments = $"-v error -select_streams a:0 -show_entries stream=codec_type -of default=noprint_wrappers=1:nokey=1 \"{filePath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc == null) return false;

            var stdout = (await proc.StandardOutput.ReadToEndAsync()).Trim();
            await proc.WaitForExitAsync();

            return !string.IsNullOrEmpty(stdout);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể kiểm tra luồng audio trong file: {File}", filePath);
            return true;
        }
    }

    private async Task<double> GetMediaDurationSecondsAsync(string filePath)
    {
        try
        {
            var ffprobe = GetFFprobePath();
            var psi = new ProcessStartInfo
            {
                FileName = ffprobe,
                Arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{filePath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc == null) return 0;

            var stdout = (await proc.StandardOutput.ReadToEndAsync()).Trim();
            await proc.WaitForExitAsync();

            if (double.TryParse(stdout, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var duration))
            {
                return duration;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể đọc thời lượng media: {File}", filePath);
        }
        return 0;
    }
}
