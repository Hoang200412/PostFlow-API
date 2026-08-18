using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using PostFlow.Services.Interfaces;

namespace PostFlow.Services
{
    public class StorageService : IStorageService
    {
        private readonly IWebHostEnvironment _env;

        public StorageService(IWebHostEnvironment env)
        {
            _env = env;
        }

        private string GetFullPath(string folderName, string fileName)
        {
            var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var directory = Path.Combine(webRoot, folderName);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            return Path.Combine(directory, fileName);
        }

        public async Task<string> SaveFileAsync(Stream fileStream, string folderName, string fileName)
        {
            var fullPath = GetFullPath(folderName, fileName);
            using (var destinationStream = new FileStream(fullPath, FileMode.Create))
            {
                await fileStream.CopyToAsync(destinationStream);
            }
            return $"/{folderName}/{fileName}";
        }

        public Task<bool> DeleteFileAsync(string fileUrlOrPath)
        {
            var relativePath = fileUrlOrPath.TrimStart('/');
            var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var fullPath = Path.Combine(webRoot, relativePath);

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                return Task.FromResult(true);
            }

            if (File.Exists(fileUrlOrPath))
            {
                File.Delete(fileUrlOrPath);
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        public Task<bool> FileExistsAsync(string fileUrlOrPath)
        {
            var relativePath = fileUrlOrPath.TrimStart('/');
            var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var fullPath = Path.Combine(webRoot, relativePath);

            return Task.FromResult(File.Exists(fullPath) || File.Exists(fileUrlOrPath));
        }

        public Task<long> GetFileSizeAsync(string fileUrlOrPath)
        {
            var relativePath = fileUrlOrPath.TrimStart('/');
            var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var fullPath = Path.Combine(webRoot, relativePath);

            if (File.Exists(fullPath))
            {
                return Task.FromResult(new FileInfo(fullPath).Length);
            }

            if (File.Exists(fileUrlOrPath))
            {
                return Task.FromResult(new FileInfo(fileUrlOrPath).Length);
            }

            return Task.FromResult(0L);
        }

        public Task<string> GeneratePresignedUploadUrlAsync(string folderName, string fileName, int expiryMinutes = 15)
        {
            // Trả về relative path làm URL giả lập để client PUT dữ liệu nhị phân lên
            return Task.FromResult($"/{folderName}/{fileName}");
        }

        public Task<string> GeneratePresignedDownloadUrlAsync(string fileUrlOrPath, int expiryHours = 1)
        {
            return Task.FromResult(fileUrlOrPath);
        }
    }
}
