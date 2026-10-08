using System.ComponentModel.DataAnnotations;

namespace PostFlow.Models.DTOs.Requests
{
    public class AnalyzeTrendRequest
    {
        [Required(ErrorMessage = "Chủ đề (Topic) không được để trống.")]
        public string Topic { get; set; } = string.Empty;

        public string Platform { get; set; } = "all";

        public string? Category { get; set; }

        public string[]? Hashtags { get; set; }

        public string? AdditionalContext { get; set; }
    }
}
