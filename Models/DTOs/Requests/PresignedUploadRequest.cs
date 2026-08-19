namespace PostFlow.Models.DTOs.Requests
{
    public class PresignedUploadRequest
    {
        public string Filename { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string ContentType { get; set; } = "video/mp4";
    }
}
