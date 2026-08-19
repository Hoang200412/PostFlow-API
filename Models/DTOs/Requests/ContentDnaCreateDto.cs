namespace PostFlow.Models.DTOs.Requests
{
    public class ContentDnaCreateDto
    {
        public string BrandName { get; set; } = string.Empty;
        public string VoiceProfile { get; set; } = string.Empty;
        public string? AvatarAssetPath { get; set; }
        public string? ScriptStructure { get; set; }
    }
}
