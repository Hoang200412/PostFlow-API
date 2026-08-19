using System;

namespace PostFlow.Models.DTOs.Requests
{
    public class VideoGenerateRequest
    {
        public string Topic { get; set; } = string.Empty;
        public string ContentDnaId { get; set; } = string.Empty;
        public string[]? ChannelIds { get; set; }
        public DateTime? ScheduledAt { get; set; }
    }
}
