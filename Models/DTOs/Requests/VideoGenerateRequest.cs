using System;
using System.ComponentModel.DataAnnotations;

namespace PostFlow.Models.DTOs.Requests
{
    public class VideoGenerateRequest
    {
        [Required(ErrorMessage = "Chủ đề (Topic) không được để trống.")]
        public string Topic { get; set; } = string.Empty;

        [Required(ErrorMessage = "Content DNA không được để trống.")]
        public string ContentDnaId { get; set; } = string.Empty;

        public string[]? ChannelIds { get; set; }
        public DateTime? ScheduledAt { get; set; }
    }
}
