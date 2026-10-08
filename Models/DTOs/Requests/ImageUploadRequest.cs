using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace PostFlow.Models.DTOs.Requests
{
    public class ImageUploadRequest
    {
        [Required(ErrorMessage = "File hình ảnh không được để trống.")]
        public IFormFile File { get; set; } = null!;
    }
}
