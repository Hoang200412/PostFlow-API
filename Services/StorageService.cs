using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using PostFlow.Services.Interfaces;

namespace PostFlow.Services
{
    /// <summary>
    /// Local Storage implementation của IStorageService.
    /// Dùng để lưu trữ file trực tiếp trên ổ đĩa local / wwwroot của server.
    /// </summary>
    public class StorageService : IStorageService
    {
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<StorageService> _logger;

        public StorageService(IWebHostEnvironment env, ILogger<StorageService> logger)
        {
            _env = env;
            _logger = logger;
        }

        private string GetFullPath(string folderName, string fileName)
        {
            var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var directory = Path.Combine(webRoot, folderName.Trim('/', '\\'));
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            return Path.Combine(directory, fileName);
        }

        private string ResolveLocalPath(string fileUrlOrPath)
        {
            if (string.IsNullOrWhiteSpace(fileUrlOrPath)) return string.Empty;

            if (File.Exists(fileUrlOrPath))
            {
                return fileUrlOrPath;
            }

            var relativePath = fileUrlOrPath.TrimStart('/', '\\');
            var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            return Path.Combine(webRoot, relativePath);
        }

        public async Task<string> SaveFileAsync(Stream fileStream, string folderName, string fileName, string? contentType = null)
        {
            var fullPath = GetFullPath(folderName, fileName);
            using (var destinationStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write))
            {
                await fileStream.CopyToAsync(destinationStream);
            }
            _logger.LogInformation("Đã lưu file vào local disk: {Path}", fullPath);
            return $"{folderName.Trim('/')}/{fileName}";
        }

        public Task<Stream> GetFileStreamAsync(string fileUrlOrPath)
        {
            var localPath = ResolveLocalPath(fileUrlOrPath);
            if (!File.Exists(localPath))
            {
                throw new FileNotFoundException($"Không tìm thấy file local tại: {fileUrlOrPath}");
            }

            Stream stream = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return Task.FromResult(stream);
        }

        public Task<string> DownloadToTempFileAsync(string fileUrlOrPath)
        {
            var localPath = ResolveLocalPath(fileUrlOrPath);
            if (File.Exists(localPath))
            {
                return Task.FromResult(localPath);
            }

            throw new FileNotFoundException($"Không tìm thấy file local tại: {fileUrlOrPath}");
        }

        public Task<bool> DeleteFileAsync(string fileUrlOrPath)
        {
            var localPath = ResolveLocalPath(fileUrlOrPath);
            if (File.Exists(localPath))
            {
                try
                {
                    File.Delete(localPath);
                    _logger.LogInformation("Đã xóa file local: {Path}", localPath);
                    return Task.FromResult(true);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi xóa file local: {Path}", localPath);
                    return Task.FromResult(false);
                }
            }

            return Task.FromResult(false);
        }

        public Task<bool> FileExistsAsync(string fileUrlOrPath)
        {
            var localPath = ResolveLocalPath(fileUrlOrPath);
            return Task.FromResult(File.Exists(localPath));
        }

        public Task<long> GetFileSizeAsync(string fileUrlOrPath)
        {
            var localPath = ResolveLocalPath(fileUrlOrPath);
            if (File.Exists(localPath))
            {
                return Task.FromResult(new FileInfo(localPath).Length);
            }

            return Task.FromResult(0L);
        }

        public Task<string> GeneratePresignedUploadUrlAsync(string folderName, string fileName, int expiryMinutes = 15, string? contentType = null)
        {
            // Trong môi trường local, trả về endpoint upload binary của backend
            var objectKey = $"{folderName.Trim('/')}/{fileName}";
            return Task.FromResult($"/api/videos/upload-binary/{fileName}");
        }

        public Task<string> GeneratePresignedDownloadUrlAsync(string fileUrlOrPath, int expiryHours = 1)
        {
            var relativePath = fileUrlOrPath.TrimStart('/');
            return Task.FromResult($"/{relativePath}");
        }
    }
}
