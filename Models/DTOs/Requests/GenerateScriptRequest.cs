using System.ComponentModel.DataAnnotations;

namespace PostFlow.Models.DTOs.Requests
{
    public class GenerateScriptRequest
    {
        [Required(ErrorMessage = "Chủ đề (Topic) không được để trống.")]
        public string Topic { get; set; } = string.Empty;

        public string? ContentDnaId { get; set; }
        public string? Tone { get; set; }
        public string? StructureId { get; set; }
        public string? CustomPrompt { get; set; }
        public string? TargetDuration { get; set; }
    }
}
