namespace PostFlow.Models.DTOs.Requests
{
    public class GenerateVoiceoverRequest
    {
        public string Text { get; set; } = string.Empty;
        public string Voice { get; set; } = "vi-VN-NamMinhNeural";
        public string? Rate { get; set; } = "+0%";
        public string? Pitch { get; set; } = "+0Hz";
    }
}
