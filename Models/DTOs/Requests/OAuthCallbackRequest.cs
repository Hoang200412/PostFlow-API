using System.ComponentModel.DataAnnotations;

namespace PostFlow.Models.DTOs.Requests
{
    public class OAuthCallbackRequest
    {
        [Required(ErrorMessage = "Authorization code không được để trống.")]
        public string Code { get; set; } = string.Empty;

        [Required(ErrorMessage = "RedirectUri không được để trống.")]
        public string RedirectUri { get; set; } = string.Empty;
    }
}
