using System.Text.Json;
using System.Text.Json.Serialization;
using PostFlow.Models.DTOs.Requests;

namespace PostFlow.Services.Explainer
{
    /// <summary>Số frame/giây của composition Remotion (phải khớp với <c>fps</c> trong RemotionEngine/src/Root.tsx).</summary>
    public static class RemotionDefaults
    {
        public const int Fps = 30;
    }

    public sealed record RemotionSubtitle(
        [property: JsonPropertyName("word")] string Word,
        [property: JsonPropertyName("start_ms")] double StartMs,
        [property: JsonPropertyName("end_ms")] double EndMs);

    public sealed record RemotionChapter(string Id, string Title, int StartFrame, int DurationInFrames);

    public sealed record RemotionTheme(string PrimaryColor, string AccentColor, string BackgroundColor)
    {
        public static RemotionTheme Finance { get; } = new("#eab308", "#10b981", "#05130f");
        public static RemotionTheme Tech { get; } = new("#3b82f6", "#8b5cf6", "#07090e");

        public static RemotionTheme ForCategory(string? category) =>
            ExplainerCategories.IsFinance(category) ? Finance : Tech;
    }

    /// <summary>Props truyền vào composition Remotion (tên thuộc tính được serialize theo camelCase).</summary>
    public sealed record RemotionRenderProps(
        string Topic,
        string Category,
        string AspectRatio,
        int DurationInFrames,
        string AudioPath,
        IReadOnlyList<RemotionSubtitle> Subtitles,
        IReadOnlyList<RemotionChapter> Chapters,
        IReadOnlyList<ExplainerSceneDto> Scenes,
        RemotionTheme Theme);

    public interface IRemotionRenderer
    {
        /// <summary>Đưa file audio vào thư mục public của RemotionEngine để <c>staticFile()</c> đọc được. Trả về đường dẫn tương đối.</summary>
        string StageAudio(string jobId, string sourceAudioPath);

        /// <summary>Xóa audio đã đưa vào thư mục public (an toàn khi gọi nhiều lần).</summary>
        void ReleaseAudio(string jobId);

        Task RenderAsync(string aspectRatio, RemotionRenderProps props, string workDirectory, string outputMp4Path, CancellationToken cancellationToken = default);
    }

    public class RemotionRenderer : IRemotionRenderer
    {
        private const string PublicDirectoryName = "public";
        private const string TempAudioDirectoryName = "temp_audio";
        private const string PortraitAspectRatio = "9:16";

        private static readonly JsonSerializerOptions PropsJsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly ExplainerToolLocator _tools;
        private readonly ILogger<RemotionRenderer> _logger;

        public RemotionRenderer(ExplainerToolLocator tools, ILogger<RemotionRenderer> logger)
        {
            _tools = tools;
            _logger = logger;
        }

        public string StageAudio(string jobId, string sourceAudioPath)
        {
            var directory = Path.Combine(_tools.RemotionEnginePath, PublicDirectoryName, TempAudioDirectoryName);
            Directory.CreateDirectory(directory);
            File.Copy(sourceAudioPath, Path.Combine(directory, AudioFileName(jobId)), overwrite: true);

            return $"{TempAudioDirectoryName}/{AudioFileName(jobId)}";
        }

        public void ReleaseAudio(string jobId)
        {
            var path = Path.Combine(_tools.RemotionEnginePath, PublicDirectoryName, TempAudioDirectoryName, AudioFileName(jobId));
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "Không thể xóa audio tạm của JobId {JobId}", jobId);
            }
        }

        public async Task RenderAsync(
            string aspectRatio,
            RemotionRenderProps props,
            string workDirectory,
            string outputMp4Path,
            CancellationToken cancellationToken = default)
        {
            var propsPath = Path.Combine(workDirectory, "remotion_props.json");
            await File.WriteAllTextAsync(propsPath, JsonSerializer.Serialize(props, PropsJsonOptions), cancellationToken);

            var composition = aspectRatio == PortraitAspectRatio ? "ExplainerPortrait" : "ExplainerLandscape";
            var arguments = new List<string>
            {
                "remotion", "render", "src/index.ts", composition, outputMp4Path,
                $"--props={propsPath}",
                $"--frames=0-{props.DurationInFrames - 1}",
                "--gl=angle"
            };

            var result = await ExternalProcessRunner.RunAsync(
                ResolveNpx(),
                arguments,
                workingDirectory: _tools.RemotionEnginePath,
                prependToPath: _tools.NodeDirectory,
                cancellationToken: cancellationToken);

            if (!result.Succeeded || !File.Exists(outputMp4Path))
            {
                _logger.LogError("Remotion render thất bại (exit {ExitCode}). Stderr: {Stderr}. Stdout: {Stdout}",
                    result.ExitCode, result.StandardError, result.StandardOutput);
                throw new InvalidOperationException($"Lỗi kết xuất Remotion: {result.StandardError}");
            }
        }

        private string ResolveNpx()
        {
            var nodeDirectory = _tools.NodeDirectory;
            if (nodeDirectory == null) return "npx";

            var npxPath = Path.Combine(nodeDirectory, "npx.cmd");
            return File.Exists(npxPath) ? npxPath : "npx";
        }

        private static string AudioFileName(string jobId) => $"{jobId}.mp3";
    }
}
