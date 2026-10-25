namespace PostFlow.Models.DTOs.Requests
{
    public class DubVideoRequest
    {
        public string VideoAssetId { get; set; } = string.Empty;
        public string? AudioUrl { get; set; }
        public string? AudioObjectKey { get; set; }
        public string? Text { get; set; }
        public string? Voice { get; set; }
        public string? Rate { get; set; }
        public string? Pitch { get; set; }
        public string AudioMode { get; set; } = "replace"; // "replace" (thay thế) hoặc "mix" (hòa trộn)
        public string? Title { get; set; }
    }
}
