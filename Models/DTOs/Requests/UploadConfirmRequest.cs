using System;

namespace PostFlow.Models.DTOs.Requests
{
    public class UploadConfirmRequest
    {
        public string VideoId { get; set; } = string.Empty;
        public string[] ChannelIds { get; set; } = Array.Empty<string>();
        public DateTime? ScheduledAt { get; set; }
    }
}
