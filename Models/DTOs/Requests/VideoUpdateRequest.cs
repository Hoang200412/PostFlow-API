using System.Collections.Generic;

namespace PostFlow.Models.DTOs.Requests
{
    public class VideoUpdateRequest
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public List<string>? Tags { get; set; }
    }
}
