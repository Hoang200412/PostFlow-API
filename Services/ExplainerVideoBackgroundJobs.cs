using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PostFlow.Data;
using PostFlow.Models;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Models.DTOs.Responses;
using PostFlow.Services.Interfaces;

namespace PostFlow.Services
{
    public class ExplainerVideoBackgroundJobs
    {
        private readonly AppDbContext _context;
        private readonly IStorageService _storageService;
        private readonly ExplainerVideoJobManager _jobManager;
        private readonly IExplainerVideoService _explainerService;
        private readonly ILogger<ExplainerVideoBackgroundJobs> _logger;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public ExplainerVideoBackgroundJobs(
            AppDbContext context,
            IStorageService storageService,
            ExplainerVideoJobManager jobManager,
            IExplainerVideoService explainerService,
            ILogger<ExplainerVideoBackgroundJobs> logger)
        {
            _context = context;
            _storageService = storageService;
            _jobManager = jobManager;
            _explainerService = explainerService;
            _logger = logger;
        }

        public async Task ResearchTopicJobAsync(string jobId, string userId, string requestJson)
        {
            try
            {
                _logger.LogInformation("Bắt đầu thực thi Background Job nghiên cứu chủ đề. JobId: {JobId}, UserId: {UserId}", jobId, userId);
                _jobManager.UpdateProgress(jobId, "Processing", 25, "Đang phân tích chuyên sâu chủ đề qua AI...");
                var request = JsonSerializer.Deserialize<ExplainerResearchRequest>(requestJson, JsonOptions);
                if (request == null)
                {
                    _jobManager.Failed(jobId, "Dữ liệu yêu cầu nghiên cứu không hợp lệ.");
                    return;
                }

                var result = await _explainerService.ResearchTopicAsync(userId, request);
                _jobManager.CompleteResearch(jobId, result);
                _logger.LogInformation("Hoàn tất Background Job nghiên cứu chủ đề. JobId: {JobId}", jobId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi trong tiến trình ResearchTopicJobAsync cho JobId: {JobId}", jobId);
                _jobManager.Failed(jobId, ex.Message);
            }
        }

        public async Task GenerateStoryboardJobAsync(string jobId, string userId, string requestJson)
        {
            try
            {
                _logger.LogInformation("Bắt đầu thực thi Background Job thiết kế phân cảnh. JobId: {JobId}, UserId: {UserId}", jobId, userId);
                _jobManager.UpdateProgress(jobId, "Processing", 25, "Đang thiết kế kịch bản & phân cảnh chuyển động qua AI...");
                var request = JsonSerializer.Deserialize<ExplainerRenderRequest>(requestJson, JsonOptions);
                if (request == null)
                {
                    _jobManager.Failed(jobId, "Dữ liệu yêu cầu phân cảnh không hợp lệ.");
                    return;
                }

                var result = await _explainerService.GenerateStoryboardAsync(userId, request);
                _jobManager.CompleteStoryboard(jobId, result);
                _logger.LogInformation("Hoàn tất Background Job thiết kế phân cảnh. JobId: {JobId}", jobId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi trong tiến trình GenerateStoryboardJobAsync cho JobId: {JobId}", jobId);
                _jobManager.Failed(jobId, ex.Message);
            }
        }

        public async Task RenderExplainerVideoAsync(string jobId, string userId, string requestJson)
        {
            var request = JsonSerializer.Deserialize<ExplainerRenderRequest>(requestJson, JsonOptions);
            if (request == null)
            {
                _jobManager.Failed(jobId, "Dữ liệu yêu cầu render không hợp lệ.");
                return;
            }

            var workDir = Path.Combine(Path.GetTempPath(), "postflow_explainer", jobId);
            Directory.CreateDirectory(workDir);

            try
            {
                // 1. Chuẩn bị Scenes
                _jobManager.UpdateProgress(jobId, "Processing", 15, "Đang thiết kế kịch bản & phân cảnh chuyển động...");
                List<ExplainerSceneDto> scenes = request.CustomScenes ?? new List<ExplainerSceneDto>();

                if (scenes.Count == 0)
                {
                    var storyboard = await _explainerService.GenerateStoryboardAsync(userId, request);
                    scenes = storyboard.Scenes;
                }

                if (scenes.Count == 0)
                {
                    throw new InvalidOperationException("Không thể tạo phân cảnh kịch bản.");
                }

                // Đảm bảo toàn bộ scenes có Type chuẩn hóa, Category và VisualIcon hợp lệ
                var isFinance = string.Equals(request.Category, "finance", StringComparison.OrdinalIgnoreCase);
                foreach (var s in scenes)
                {
                    s.Type = (s.Type ?? "hero").ToLower().Trim();
                    s.Category = request.Category ?? "tech";
                    if (string.IsNullOrWhiteSpace(s.VisualIcon))
                    {
                        s.VisualIcon = isFinance ? "dollar" : "sparkles";
                    }
                }

                // 2. Sinh giọng đọc Edge-TTS kèm Word Timestamps
                _jobManager.UpdateProgress(jobId, "GeneratingAudio", 35, "Đang sinh giọng đọc tiếng Việt & đồng bộ thời gian từng từ...");
                
                var fullNarration = string.Join(" ", scenes.Select(s => s.Narration?.Trim()).Where(s => !string.IsNullOrEmpty(s)));
                if (string.IsNullOrWhiteSpace(fullNarration))
                {
                    fullNarration = request.Topic;
                }

                // Tiền xử lý lời thoại tiếng Việt: loại bỏ emoji, chuẩn hóa số thập phân, ký hiệu phần trăm và dấu hai chấm
                fullNarration = Regex.Replace(fullNarration, @"[\uD800-\uDBFF][\uDC00-\uDFFF]", " ");
                fullNarration = Regex.Replace(fullNarration, @"[⚡📈📉🛡️💰🔥★☆✓✕•➔→←↑↓©®™■□▲▼▶◀◆◇●○]", " ");
                fullNarration = Regex.Replace(fullNarration, @"(\d+)\s*%", "$1 phần trăm");
                fullNarration = fullNarration.Replace("%", " phần trăm ");
                fullNarration = Regex.Replace(fullNarration, @"[:;]", ", ");
                fullNarration = Regex.Replace(fullNarration, @"(\d+)\.(\d+)", "$1,$2");
                fullNarration = Regex.Replace(fullNarration, @"(\d+)/(\d+)", "$1 trên $2");
                fullNarration = Regex.Replace(fullNarration, @"\s+", " ").Trim();
                if (!string.IsNullOrEmpty(fullNarration) && !fullNarration.EndsWith(".") && !fullNarration.EndsWith("!") && !fullNarration.EndsWith("?"))
                {
                    fullNarration += ".";
                }

                var textFilePath = Path.Combine(workDir, "narration.txt");
                await File.WriteAllTextAsync(textFilePath, fullNarration, new UTF8Encoding(false));

                var audioOutputPath = Path.Combine(workDir, "narration.mp3");
                var timingOutputPath = Path.Combine(workDir, "timing.json");

                await RunEdgeTtsAlignerAsync(textFilePath, request.Voice, request.Rate, request.Pitch, audioOutputPath, timingOutputPath);

                // 3. Phân tích timing và phân bổ frames cho từng Scene khớp 100% với giọng đọc Edge-TTS
                _jobManager.UpdateProgress(jobId, "Aligning", 50, "Đang phân bổ nhịp phân cảnh theo audio...");
                
                var timingJsonContent = await File.ReadAllTextAsync(timingOutputPath);
                using var timingDoc = JsonDocument.Parse(timingJsonContent);
                var totalDurationSeconds = timingDoc.RootElement.GetProperty("total_duration_seconds").GetDouble();
                var fps = 30;

                // Thêm 0.5s (15 frames) đệm cuối cùng để âm thanh kết thúc tự nhiên, không bị cụt câu
                var totalFrames = Math.Max(90, (int)Math.Ceiling((totalDurationSeconds + 0.5) * fps));

                // Phân bổ thời lượng cho từng scene theo số từ của lời thoại
                var wordCounts = scenes.Select(s =>
                {
                    var words = (s.Narration ?? "").Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    return Math.Max(1, words.Length);
                }).ToList();
                var totalWords = Math.Max(1, wordCounts.Sum());

                var currentFrame = 0;
                var chapters = new List<object>();
                int allocatedFrames = 0;

                for (int i = 0; i < scenes.Count; i++)
                {
                    var s = scenes[i];
                    int sceneFrames;

                    if (i == scenes.Count - 1)
                    {
                        // Scene cuối cùng nhận toàn bộ số frames còn lại để tổng khớp chính xác 100% với totalFrames
                        sceneFrames = Math.Max(20, totalFrames - allocatedFrames);
                    }
                    else
                    {
                        var rawFrames = (int)Math.Round((double)wordCounts[i] / totalWords * totalFrames);
                        int remainingScenes = scenes.Count - 1 - i;
                        int minFramesForRemaining = remainingScenes * 20;
                        int maxFrames = Math.Max(20, totalFrames - allocatedFrames - minFramesForRemaining);
                        
                        sceneFrames = Math.Clamp(rawFrames, 20, maxFrames);
                    }

                    s.DurationInFrames = sceneFrames;
                    allocatedFrames += sceneFrames;

                    chapters.Add(new
                    {
                        id = $"c_{i + 1}",
                        title = s.Title,
                        startFrame = currentFrame,
                        durationInFrames = s.DurationInFrames
                    });

                    currentFrame += s.DurationInFrames;
                }

                // Cập nhật totalFrames chuẩn hóa theo tổng frames của các scene
                totalFrames = currentFrame;

                // 4. Chuẩn bị props JSON cho Remotion Engine
                _jobManager.UpdateProgress(jobId, "PreparingProps", 65, "Đang khởi tạo tham số Remotion Engine...");
                
                var wordsList = new List<object>();
                if (timingDoc.RootElement.TryGetProperty("words", out var wordsElement))
                {
                    foreach (var w in wordsElement.EnumerateArray())
                    {
                        wordsList.Add(new
                        {
                            word = w.GetProperty("text").GetString(),
                            start_ms = w.GetProperty("start_ms").GetDouble(),
                            end_ms = w.GetProperty("end_ms").GetDouble()
                        });
                    }
                }

                // Sao chép file audio vào thư mục public của RemotionEngine để staticFile() đọc được
                var remotionPublicAudioDir = Path.Combine(GetRemotionEnginePath(), "public", "temp_audio");
                Directory.CreateDirectory(remotionPublicAudioDir);
                var audioRelName = $"temp_audio/{jobId}.mp3";
                var audioDestPath = Path.Combine(GetRemotionEnginePath(), "public", audioRelName.Replace("/", "\\"));
                File.Copy(audioOutputPath, audioDestPath, true);

                var remotionProps = new
                {
                    topic = request.Topic,
                    category = request.Category ?? "tech",
                    aspectRatio = request.AspectRatio,
                    durationInFrames = totalFrames,
                    audioPath = audioRelName,
                    subtitles = wordsList,
                    chapters,
                    scenes,
                    theme = isFinance
                        ? new
                        {
                            primaryColor = "#eab308", // Luxurious Gold
                            accentColor = "#10b981",  // Wealth Emerald
                            backgroundColor = "#05130f" // Deep Emerald Dark
                        }
                        : new
                        {
                            primaryColor = "#3b82f6", // Electric Blue
                            accentColor = "#8b5cf6",  // Cyber Purple
                            backgroundColor = "#07090e" // Deep Cyber Dark
                        }
                };

                var propsPath = Path.Combine(workDir, "remotion_props.json");
                await File.WriteAllTextAsync(propsPath, JsonSerializer.Serialize(remotionProps, JsonOptions), new UTF8Encoding(false));

                // 5. Kết xuất Video qua Remotion Headless Renderer
                _jobManager.UpdateProgress(jobId, "RenderingVideo", 75, "Đang kết xuất video Remotion 1080p bằng Chromium...");
                
                var finalMp4Path = Path.Combine(workDir, "final_explainer.mp4");
                var compositionName = request.AspectRatio == "9:16" ? "ExplainerPortrait" : "ExplainerLandscape";

                await RunRemotionRenderAsync(compositionName, propsPath, finalMp4Path, totalFrames);

                // 6. Tải video thành phẩm lên Cloudflare R2 / Storage
                _jobManager.UpdateProgress(jobId, "Uploading", 90, "Đang tải video hoàn chỉnh lên lưu trữ...");

                var videoTitle = !string.IsNullOrWhiteSpace(request.Title) ? request.Title : $"Explainer: {request.Topic}";
                var videoAsset = new VideoAsset
                {
                    UserId = userId,
                    Title = videoTitle,
                    Description = $"Video đồ họa chuyển động Remotion giải thích chủ đề: {request.Topic}",
                    Status = "Ready",
                    DurationSeconds = (int)Math.Round((double)totalFrames / fps)
                };

                await using (var fileStream = File.OpenRead(finalMp4Path))
                {
                    var fileName = $"{Guid.NewGuid():N}.mp4";
                    videoAsset.LocalFilePath = await _storageService.SaveFileAsync(fileStream, $"videos/{userId}", fileName, "video/mp4");
                }

                _context.VideoAssets.Add(videoAsset);
                await _context.SaveChangesAsync();

                var downloadUrl = await _storageService.GeneratePresignedDownloadUrlAsync(videoAsset.LocalFilePath);

                // 7. Hoàn tất Job
                _jobManager.Completed(jobId, videoAsset.Id, downloadUrl);
                _logger.LogInformation("Render Explainer Video thành công. JobId: {JobId}, VideoAssetId: {VideoId}", jobId, videoAsset.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xử lý render Explainer Video JobId: {JobId}", jobId);
                _jobManager.Failed(jobId, ex.Message);
            }
            finally
            {
                // Dọn dẹp thư mục tạm
                try
                {
                    if (Directory.Exists(workDir)) Directory.Delete(workDir, true);
                    var audioDestPath = Path.Combine(GetRemotionEnginePath(), "public", "temp_audio", $"{jobId}.mp3");
                    if (File.Exists(audioDestPath)) File.Delete(audioDestPath);
                }
                catch { }
            }
        }

        private async Task RunEdgeTtsAlignerAsync(string textFilePath, string? voice, string? rate, string? pitch, string audioOut, string jsonOut)
        {
            var validVoice = string.IsNullOrWhiteSpace(voice) ? "vi-VN-NamMinhNeural" : voice.Trim();
            var validRate = string.IsNullOrWhiteSpace(rate) ? "+0%" : rate.Trim();
            var validPitch = string.IsNullOrWhiteSpace(pitch) ? "+0Hz" : pitch.Trim();

            var alignerScript = Path.Combine(Directory.GetCurrentDirectory(), "Tools", "edge_tts_aligner.py");
            if (!File.Exists(alignerScript))
            {
                alignerScript = @"D:\Đồ án\PostFlow-API\Tools\edge_tts_aligner.py";
            }

            var psi = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = $"\"{alignerScript}\" --text-file \"{textFilePath}\" --voice \"{validVoice}\" --rate \"{validRate}\" --pitch \"{validPitch}\" --output-audio \"{audioOut}\" --output-json \"{jsonOut}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) throw new InvalidOperationException("Không thể khởi động tiến trình Python Edge-TTS Aligner.");

            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0 || !File.Exists(audioOut) || !File.Exists(jsonOut))
            {
                _logger.LogError("Lỗi edge_tts_aligner.py: {Stderr}", stderr);
                throw new InvalidOperationException($"Lỗi sinh audio Edge-TTS: {stderr}");
            }
        }

        private async Task RunRemotionRenderAsync(string compositionName, string propsPath, string outputMp4Path, int totalFrames)
        {
            var enginePath = GetRemotionEnginePath();
            var nodeDir = @"C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Microsoft\VisualStudio\NodeJs";
            var npxPath = Path.Combine(nodeDir, "npx.cmd");
            var fileName = File.Exists(npxPath) ? npxPath : "npx";

            var framesArg = totalFrames > 0 ? $"--frames=0-{totalFrames - 1} " : "";

            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = $"remotion render src/index.ts {compositionName} \"{outputMp4Path}\" --props=\"{propsPath}\" {framesArg}--gl=angle",
                WorkingDirectory = enginePath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            if (Directory.Exists(nodeDir))
            {
                var existingPath = Environment.GetEnvironmentVariable("PATH") ?? "";
                psi.EnvironmentVariables["PATH"] = $"{nodeDir};{existingPath}";
            }

            using var process = Process.Start(psi);
            if (process == null) throw new InvalidOperationException("Không thể khởi động Remotion CLI Renderer.");

            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0 || !File.Exists(outputMp4Path))
            {
                _logger.LogError("Lỗi Remotion render: {Stderr}, Stdout: {Stdout}", stderr, stdout);
                throw new InvalidOperationException($"Lỗi kết xuất Remotion: {stderr}");
            }
        }

        private static string GetRemotionEnginePath()
        {
            var localPath = Path.Combine(Directory.GetCurrentDirectory(), "RemotionEngine");
            if (Directory.Exists(localPath)) return localPath;

            return @"D:\Đồ án\PostFlow-API\RemotionEngine";
        }
    }
}
