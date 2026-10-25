namespace PostFlow.Models.DTOs.Responses
{
    public class VoiceoverPreviewResponse
    {
        public string AudioUrl { get; set; } = string.Empty;
        public string AudioObjectKey { get; set; } = string.Empty;
        public double DurationSeconds { get; set; }
        public string Voice { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }
}
