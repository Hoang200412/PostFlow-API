using PostFlow.Models;

namespace PostFlow.Models.DTOs.Requests
{
    public class LocalPathUploadRequest
    {
        public string VideoFilePath { get; set; } = string.Empty;
        public PlatformType Platform { get; set; }
        public VideoMetadata? Metadata { get; set; }
    }
}
