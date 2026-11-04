using System;
using System.Collections.Concurrent;
using System.Linq;
using PostFlow.Models.DTOs.Responses;
using PostFlow.Services.Interfaces;

namespace PostFlow.Services
{
    public class VoiceoverJobManager : IVoiceoverJobManager
    {
        private readonly ConcurrentDictionary<string, VoiceoverJobStatusDto> _jobs = new();

        public void CreateJob(string jobId, string userId)
        {
            CleanupOldJobs();
            _jobs[jobId] = new VoiceoverJobStatusDto
            {
                JobId = jobId,
                UserId = userId,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };
        }

        public void SetProcessing(string jobId)
        {
            if (_jobs.TryGetValue(jobId, out var job))
            {
                job.Status = "Processing";
            }
        }

        public void SetCompleted(string jobId, VoiceoverPreviewResponse result)
        {
            if (_jobs.TryGetValue(jobId, out var job))
            {
                job.Status = "Completed";
                job.Result = result;
                job.CompletedAt = DateTime.UtcNow;
            }
        }

        public void SetFailed(string jobId, string errorMessage)
        {
            if (_jobs.TryGetValue(jobId, out var job))
            {
                job.Status = "Failed";
                job.ErrorMessage = errorMessage;
                job.CompletedAt = DateTime.UtcNow;
            }
        }

        public VoiceoverJobStatusDto? GetJob(string jobId, string userId)
        {
            if (_jobs.TryGetValue(jobId, out var job))
            {
                if (job.UserId == userId)
                {
                    return job;
                }
            }
            return null;
        }

        private void CleanupOldJobs()
        {
            var cutoff = DateTime.UtcNow.AddHours(-2);
            foreach (var kvp in _jobs.Where(x => x.Value.CreatedAt < cutoff).ToList())
            {
                _jobs.TryRemove(kvp.Key, out _);
            }
        }
    }
}

