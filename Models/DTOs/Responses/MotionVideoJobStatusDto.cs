namespace PostFlow.Models.DTOs.Responses;

public class MotionVideoJobStatusDto
{
    public string JobId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public int CurrentScene { get; set; }
    public int TotalScenes { get; set; }
    public int Progress { get; set; }
    public string? VideoId { get; set; }
    public string? VideoUrl { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
