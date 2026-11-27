using System.ComponentModel.DataAnnotations;

namespace PostFlow.Models.DTOs.Requests;

public class UpsertTtsVoiceRequest
{
    [Required(ErrorMessage = "Mã giọng đọc (VoiceId) không được để trống.")]
    [MaxLength(100)]
    public string VoiceId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tên hiển thị không được để trống.")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Gender { get; set; } = "female";

    [MaxLength(50)]
    public string Language { get; set; } = "Tiếng Việt";

    [MaxLength(100)]
    public string Accent { get; set; } = "Tiếng Việt chuẩn";

    [MaxLength(50)]
    public string Provider { get; set; } = "Kokoro TTS";

    [MaxLength(300)]
    public string? SampleText { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; } = false;
    public int SortOrder { get; set; } = 0;
}
