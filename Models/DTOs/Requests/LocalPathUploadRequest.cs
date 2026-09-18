using System.ComponentModel.DataAnnotations;
using PostFlow.Models;

namespace PostFlow.Models.DTOs.Requests
{
    public class LocalPathUploadRequest
    {
        [Required(ErrorMessage = "Đường dẫn file video không được để trống.")]
        public string VideoFilePath { get; set; } = string.Empty;

        public PlatformType Platform { get; set; }
        public VideoMetadata? Metadata { get; set; }
    }
}
