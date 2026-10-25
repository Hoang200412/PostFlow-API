using System;
using PostFlow.Models.DTOs.Responses;

namespace PostFlow.Services.Interfaces
{
    public class ScriptJobStatusDto
    {
        public string JobId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending"; // "Pending", "Processing", "Completed", "Failed"
        public ScriptDto? Script { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
    }

    public interface IScriptJobManager
    {
        void CreateJob(string jobId, string userId);
        void SetProcessing(string jobId);
        void SetCompleted(string jobId, ScriptDto script);
        void SetFailed(string jobId, string errorMessage);
        ScriptJobStatusDto? GetJob(string jobId, string userId);
    }
}
