using System.IO;
using System.Threading.Tasks;

namespace PostFlow.Services.Interfaces
{
    public interface IStorageService
    {
        Task<string> SaveFileAsync(Stream fileStream, string folderName, string fileName, string? contentType = null);
        Task<Stream> GetFileStreamAsync(string fileUrlOrPath);
        Task<string> DownloadToTempFileAsync(string fileUrlOrPath);
        Task<bool> DeleteFileAsync(string fileUrlOrPath);
        Task<bool> FileExistsAsync(string fileUrlOrPath);
        Task<long> GetFileSizeAsync(string fileUrlOrPath);
        Task<string> GeneratePresignedUploadUrlAsync(string folderName, string fileName, int expiryMinutes = 15, string? contentType = null);
        Task<string> GeneratePresignedDownloadUrlAsync(string fileUrlOrPath, int expiryHours = 1);
    }
}
