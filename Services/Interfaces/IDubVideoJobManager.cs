using System;
using PostFlow.Models.DTOs.Responses;

namespace PostFlow.Services.Interfaces;

public class DubVideoJobStatusDto
{
    public string JobId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public VideoAssetDto? Result { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}

public interface IDubVideoJobManager
{
    void CreateJob(string jobId, string userId);
    void SetProcessing(string jobId);
    void SetCompleted(string jobId, VideoAssetDto result);
    void SetFailed(string jobId, string errorMessage);
    DubVideoJobStatusDto? GetJob(string jobId, string userId);
}