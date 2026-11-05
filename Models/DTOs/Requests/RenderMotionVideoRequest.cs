using System.ComponentModel.DataAnnotations;

namespace PostFlow.Models.DTOs.Requests;

public class RenderMotionVideoRequest
{
    [Required, MaxLength(200)]
    public string Title { get; set; } = "Video động mới";

    [MaxLength(500)]
    public string Topic { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Voice { get; set; }

    [MaxLength(20)]
    public string? Rate { get; set; } = "+0%";

    [MaxLength(20)]
    public string? Pitch { get; set; } = "+0Hz";

    [MaxLength(20)]
    public string SubtitleMode { get; set; } = "basic";

    [MaxLength(2000)]
    public string? BackgroundMusicObjectKey { get; set; }

    [MaxLength(20)]
    public string AudioMode { get; set; } = "voice-only";

    [Required, MinLength(1), MaxLength(20)]
    public List<MotionSceneRequest> Scenes { get; set; } = new();
}

public class MotionSceneRequest
{
    [MaxLength(5000)]
    public string Narration { get; set; } = string.Empty;

    [MaxLength(200)]
    public string VisualKeyword { get; set; } = string.Empty;

    [Range(1, 120)]
    public int DurationSeconds { get; set; } = 5;

    [MaxLength(2000)]
    public string? VideoUrl { get; set; }
    [MaxLength(20)]
    public string Provider { get; set; } = "pexels";
    [MaxLength(2000)]
    public string? ObjectKey { get; set; }

    [MaxLength(40)]
    public string MotionPreset { get; set; } = "auto";

    [MaxLength(200)]
    public string? SourceUrl { get; set; }
}
