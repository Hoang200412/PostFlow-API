namespace PostFlow.Models.DTOs.Requests
{
    public class PlatformCallbackRequest
    {
        public string JobId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? VideoUrl { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
