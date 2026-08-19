using System;

namespace PostFlow.Models.DTOs.Requests
{
    public class PostingJobUpdateRequest
    {
        public DateTime? ScheduledAt { get; set; }
        public string? PlatformMetadataOverride { get; set; }
    }
}
