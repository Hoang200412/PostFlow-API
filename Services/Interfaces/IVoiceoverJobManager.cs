using System;
using PostFlow.Models.DTOs.Responses;

namespace PostFlow.Services.Interfaces
{
    public class VoiceoverJobStatusDto
    {
        public string JobId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending"; // "Pending", "Processing", "Completed", "Failed"
        public VoiceoverPreviewResponse? Result { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
    }

    public interface IVoiceoverJobManager
    {
        void CreateJob(string jobId, string userId);
        void SetProcessing(string jobId);
        void SetCompleted(string jobId, VoiceoverPreviewResponse result);
        void SetFailed(string jobId, string errorMessage);
        VoiceoverJobStatusDto? GetJob(string jobId, string userId);
    }
}

