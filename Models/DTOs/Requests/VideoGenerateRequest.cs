using System;
using System.ComponentModel.DataAnnotations;
using PostFlow.Models.DTOs.Responses;

namespace PostFlow.Models.DTOs.Requests
{
    public class VideoGenerateRequest
    {
        [Required(ErrorMessage = "Chủ đề (Topic) không được để trống.")]
        public string Topic { get; set; } = string.Empty;

        public string? ContentDnaId { get; set; }

        public string? StructureId { get; set; }

        [Required(ErrorMessage = "Kịch bản không được để trống.")]
        public ScriptDto Script { get; set; } = new();

        public string? Voice { get; set; }

        public string[]? ChannelIds { get; set; }
        public DateTime? ScheduledAt { get; set; }
    }
}
