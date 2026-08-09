using System.ComponentModel.DataAnnotations;

namespace PostFlow.Models.DTOs.Requests
{
    public class TokenRequestDto
    {
        [Required(ErrorMessage = "Access Token không được để trống.")]
        public string AccessToken { get; set; } = string.Empty;

        [Required(ErrorMessage = "Refresh Token không được để trống.")]
        public string RefreshToken { get; set; } = string.Empty;
    }
}
