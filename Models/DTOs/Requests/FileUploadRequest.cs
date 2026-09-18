using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using PostFlow.Models;

namespace PostFlow.Models.DTOs.Requests
{
    public class FileUploadRequest
    {
        [Required(ErrorMessage = "Vui lòng chọn file video hợp lệ.")]
        public IFormFile File { get; set; } = null!;

        public PlatformType Platform { get; set; } = PlatformType.YouTube;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string PrivacyStatus { get; set; } = "public";
    }
}
