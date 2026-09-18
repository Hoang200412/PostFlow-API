using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace PostFlow.Models.DTOs.Requests
{
    public class VideoUploadRequest
    {
        [Required(ErrorMessage = "Vui lòng chọn file video hợp lệ.")]
        public IFormFile File { get; set; } = null!;

        public string? Title { get; set; }

        public string? Description { get; set; }

        public string? Tags { get; set; }

        public List<string> GetTagList()
        {
            if (string.IsNullOrWhiteSpace(Tags)) return new List<string>();
            return new List<string>(Tags.Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries));
        }
    }
}
