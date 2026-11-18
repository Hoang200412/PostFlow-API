using System.Diagnostics;

namespace PostFlow.Services.Explainer
{
    public readonly record struct ProcessResult(int ExitCode, string StandardOutput, string StandardError)
    {
        public bool Succeeded => ExitCode == 0;
    }

    internal static class ExternalProcessRunner
    {
        /// <summary>
        /// Chạy tiến trình ngoài, đọc stdout/stderr song song để không bị treo khi buffer đầy.
        /// Tham số truyền qua <see cref="ProcessStartInfo.ArgumentList"/> nên không cần tự quote (tránh injection).
        /// </summary>
        public static async Task<ProcessResult> RunAsync(
            string fileName,
            IEnumerable<string> arguments,
            string? workingDirectory = null,
            string? prependToPath = null,
            CancellationToken cancellationToken = default)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            if (!string.IsNullOrEmpty(workingDirectory))
            {
                startInfo.WorkingDirectory = workingDirectory;
            }

            if (!string.IsNullOrEmpty(prependToPath))
            {
                var currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
                startInfo.EnvironmentVariables["PATH"] = $"{prependToPath}{Path.PathSeparator}{currentPath}";
            }

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException($"Không thể khởi động tiến trình '{fileName}'.");

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            return new ProcessResult(process.ExitCode, await stdoutTask, await stderrTask);
        }
    }
}
