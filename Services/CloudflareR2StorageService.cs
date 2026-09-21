using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PostFlow.Configuration;
using PostFlow.Services.Interfaces;

namespace PostFlow.Services
{
    public class CloudflareR2StorageService : IStorageService
    {
        private readonly CloudflareR2Settings _settings;
        private readonly IAmazonS3 _s3Client;
        private readonly ILogger<CloudflareR2StorageService> _logger;

        public CloudflareR2StorageService(
            IOptions<CloudflareR2Settings> settings,
            ILogger<CloudflareR2StorageService> logger)
        {
            _settings = settings.Value;
            _logger = logger;

            var credentials = new BasicAWSCredentials(_settings.AccessKeyId, _settings.SecretAccessKey);
            var config = new AmazonS3Config
            {
                ServiceURL = _settings.ServiceUrl,
                ForcePathStyle = true,
                AuthenticationRegion = "auto",
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
            };

            _s3Client = new AmazonS3Client(credentials, config);
            _logger.LogInformation("Đã khởi tạo AmazonS3Client kết nối Cloudflare R2: Endpoint={Endpoint}, Bucket={Bucket}", _settings.ServiceUrl, _settings.BucketName);
        }

        private static string NormalizeObjectKey(string fileUrlOrPath)
        {
            if (string.IsNullOrWhiteSpace(fileUrlOrPath)) return string.Empty;

            var key = fileUrlOrPath.Trim();

            if (Uri.TryCreate(key, UriKind.Absolute, out var uri))
            {
                key = uri.AbsolutePath.TrimStart('/');
            }
            else
            {
                key = key.TrimStart('/');
            }

            return key;
        }

        public async Task<string> SaveFileAsync(Stream fileStream, string folderName, string fileName, string? contentType = null)
        {
            var objectKey = string.IsNullOrWhiteSpace(folderName)
                ? fileName
                : $"{folderName.Trim('/')}/{fileName}";

            _logger.LogInformation("Đang tải file lên Cloudflare R2: {Key}", objectKey);

            var putRequest = new PutObjectRequest
            {
                BucketName = _settings.BucketName,
                Key = objectKey,
                InputStream = fileStream,
                ContentType = contentType ?? "application/octet-stream",
                DisablePayloadSigning = true
            };

            await _s3Client.PutObjectAsync(putRequest);
            _logger.LogInformation("Tải file lên Cloudflare R2 thành công: {Key}", objectKey);

            return objectKey;
        }

        public async Task<Stream> GetFileStreamAsync(string fileUrlOrPath)
        {
            var objectKey = NormalizeObjectKey(fileUrlOrPath);

            var getRequest = new GetObjectRequest
            {
                BucketName = _settings.BucketName,
                Key = objectKey
            };

            var response = await _s3Client.GetObjectAsync(getRequest);
            var memoryStream = new MemoryStream();
            await response.ResponseStream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;
            return memoryStream;
        }

        public async Task<string> DownloadToTempFileAsync(string fileUrlOrPath)
        {
            var tempDir = Path.Combine(Directory.GetCurrentDirectory(), "temp_cache");
            if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);

            var objectKey = NormalizeObjectKey(fileUrlOrPath);
            var ext = Path.GetExtension(objectKey);
            if (string.IsNullOrEmpty(ext)) ext = ".mp4";

            var tempFilePath = Path.Combine(tempDir, $"{Guid.NewGuid():N}{ext}");

            using var stream = await GetFileStreamAsync(fileUrlOrPath);
            using var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write);
            await stream.CopyToAsync(fileStream);

            _logger.LogInformation("Đã tải file từ Cloudflare R2 về cache server: {TempPath}", tempFilePath);
            return tempFilePath;
        }

        public async Task<bool> DeleteFileAsync(string fileUrlOrPath)
        {
            var objectKey = NormalizeObjectKey(fileUrlOrPath);

            try
            {
                var deleteRequest = new DeleteObjectRequest
                {
                    BucketName = _settings.BucketName,
                    Key = objectKey
                };
                await _s3Client.DeleteObjectAsync(deleteRequest);
                _logger.LogInformation("Đã xóa file trên Cloudflare R2: {Key}", objectKey);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xóa file trên Cloudflare R2: {Key}", objectKey);
                return false;
            }
        }

        public async Task<bool> FileExistsAsync(string fileUrlOrPath)
        {
            var objectKey = NormalizeObjectKey(fileUrlOrPath);

            try
            {
                var metaRequest = new GetObjectMetadataRequest
                {
                    BucketName = _settings.BucketName,
                    Key = objectKey
                };
                await _s3Client.GetObjectMetadataAsync(metaRequest);
                return true;
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound || ex.ErrorCode == "NoSuchKey")
            {
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Lỗi kiểm tra FileExists trên Cloudflare R2: {Key} - {Msg}", objectKey, ex.Message);
                return false;
            }
        }

        public async Task<long> GetFileSizeAsync(string fileUrlOrPath)
        {
            var objectKey = NormalizeObjectKey(fileUrlOrPath);

            try
            {
                var metaRequest = new GetObjectMetadataRequest
                {
                    BucketName = _settings.BucketName,
                    Key = objectKey
                };
                var response = await _s3Client.GetObjectMetadataAsync(metaRequest);
                return response.ContentLength;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Lỗi lấy kích thước file trên Cloudflare R2: {Key} - {Msg}", objectKey, ex.Message);
                return 0L;
            }
        }

        public Task<string> GeneratePresignedUploadUrlAsync(string folderName, string fileName, int expiryMinutes = 15, string? contentType = null)
        {
            var objectKey = string.IsNullOrWhiteSpace(folderName)
                ? fileName
                : $"{folderName.Trim('/')}/{fileName}";

            var request = new GetPreSignedUrlRequest
            {
                BucketName = _settings.BucketName,
                Key = objectKey,
                Verb = HttpVerb.PUT,
                Expires = DateTime.UtcNow.AddMinutes(expiryMinutes > 0 ? expiryMinutes : _settings.PresignedUrlExpiryMinutes)
            };

            if (!string.IsNullOrWhiteSpace(contentType))
            {
                request.ContentType = contentType;
            }

            string presignedUrl = _s3Client.GetPreSignedURL(request);
            _logger.LogInformation("Đã sinh Cloudflare R2 Presigned Upload URL cho {Key}", objectKey);
            return Task.FromResult(presignedUrl);
        }

        public Task<string> GeneratePresignedDownloadUrlAsync(string fileUrlOrPath, int expiryHours = 1)
        {
            var objectKey = NormalizeObjectKey(fileUrlOrPath);

            if (!string.IsNullOrWhiteSpace(_settings.PublicCustomDomain))
            {
                var domain = _settings.PublicCustomDomain.TrimEnd('/');
                return Task.FromResult($"{domain}/{objectKey}");
            }

            var request = new GetPreSignedUrlRequest
            {
                BucketName = _settings.BucketName,
                Key = objectKey,
                Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow.AddHours(expiryHours)
            };

            string presignedUrl = _s3Client.GetPreSignedURL(request);
            return Task.FromResult(presignedUrl);
        }
    }
}
