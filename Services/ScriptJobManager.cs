using System;
using System.Collections.Concurrent;
using System.Linq;
using PostFlow.Models.DTOs.Responses;
using PostFlow.Services.Interfaces;

namespace PostFlow.Services
{
    public class ScriptJobManager : IScriptJobManager
    {
        private readonly ConcurrentDictionary<string, ScriptJobStatusDto> _jobs = new();

        public void CreateJob(string jobId, string userId)
        {
            CleanupOldJobs();
            _jobs[jobId] = new ScriptJobStatusDto
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

        public void SetCompleted(string jobId, ScriptDto script)
        {
            if (_jobs.TryGetValue(jobId, out var job))
            {
                job.Status = "Completed";
                job.Script = script;
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

        public ScriptJobStatusDto? GetJob(string jobId, string userId)
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
