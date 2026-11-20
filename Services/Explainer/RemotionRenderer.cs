using System.Text.Json;
using System.Text.Json.Serialization;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Services.Interfaces;

namespace PostFlow.Services.Explainer
{
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
