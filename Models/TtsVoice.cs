using System;
using System.ComponentModel.DataAnnotations;

namespace PostFlow.Models;

/// <summary>
/// Danh sách giọng đọc TTS (Kokoro-Vietnamese) lưu trong DB, Admin có thể thêm/bật/tắt.
/// </summary>
public class TtsVoice
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Mã giọng trên PostFlow-TTS (vd: diem_trinh, hung_thinh).</summary>
    [Required]
    [MaxLength(100)]
    public string VoiceId { get; set; } = string.Empty;

    /// <summary>Tên hiển thị cho người dùng (vd: Diễm Trinh).</summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Giới tính: female | male.</summary>
    [Required]
    [MaxLength(20)]
    public string Gender { get; set; } = "female";

    [MaxLength(50)]
    public string Language { get; set; } = "Tiếng Việt";

    [MaxLength(100)]
    public string Accent { get; set; } = "Tiếng Việt chuẩn";

    [MaxLength(50)]
    public string Provider { get; set; } = "Kokoro TTS";

    /// <summary>Câu mẫu dùng để nghe thử giọng (vd: "Xin chào, tôi là Diễm Trinh.").</summary>
    [MaxLength(300)]
    public string SampleText { get; set; } = string.Empty;

    /// <summary>Object key của file audio mẫu trên Cloudflare R2 (vd: audio/voices/diem_trinh.mp3).</summary>
    [MaxLength(500)]
    public string? SampleAudioObjectKey { get; set; }

    /// <summary>URL công khai của file audio mẫu trên Cloudflare R2 để nghe thử trực tiếp.</summary>
    [MaxLength(1000)]
    public string? SampleAudioUrl { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; } = false;
    public int SortOrder { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
