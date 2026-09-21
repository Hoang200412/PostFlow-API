namespace PostFlow.Configuration
{
    public class CloudflareR2Settings
    {
        private string? _serviceUrl;

        public string AccountId { get; set; } = string.Empty;
        public string AccessKeyId { get; set; } = string.Empty;
        public string SecretAccessKey { get; set; } = string.Empty;
        public string BucketName { get; set; } = string.Empty;
        public string? PublicCustomDomain { get; set; }
        public int PresignedUrlExpiryMinutes { get; set; } = 30;
        public int MaxFileSizeMB { get; set; } = 500;

        /// <summary>
        /// Cho phép ghi đè ServiceURL (ví dụ endpoint EU: https://<AccountId>.eu.r2.cloudflarestorage.com)
        /// hoặc tự động sinh từ AccountId nếu chưa cấu hình.
        /// </summary>
        public string ServiceUrl
        {
            get => !string.IsNullOrWhiteSpace(_serviceUrl)
                ? _serviceUrl
                : (!string.IsNullOrWhiteSpace(AccountId) ? $"https://{AccountId}.r2.cloudflarestorage.com" : string.Empty);
            set => _serviceUrl = value;
        }

        public long MaxFileSizeBytes => (long)MaxFileSizeMB * 1024 * 1024;
    }
}
