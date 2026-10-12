using System.ComponentModel.DataAnnotations;

namespace PostFlow.Models.DTOs.Requests;

public sealed class SocialLoginCallbackRequest
{
    [Required]
    public string Code { get; set; } = string.Empty;

    [Required]
    public string RedirectUri { get; set; } = string.Empty;

    public string? State { get; set; }
}
