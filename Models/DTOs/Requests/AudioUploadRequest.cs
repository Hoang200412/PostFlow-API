using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace PostFlow.Models.DTOs.Requests
{
    public class AudioUploadRequest
    {
        [Required(ErrorMessage = "File âm thanh không được để trống.")]
        public IFormFile File { get; set; } = null!;
    }
}

