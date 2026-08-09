namespace PostFlow.Models.DTOs.Requests
{
    public class EnqueueUploadRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string VideoAssetId { get; set; } = string.Empty;
        public string SocialAccountId { get; set; } = string.Empty;
    }
}
