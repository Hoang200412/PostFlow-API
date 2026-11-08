using System;
using System.Collections.Concurrent;
using System.Linq;
using PostFlow.Models.DTOs.Responses;
using PostFlow.Services.Interfaces;

namespace PostFlow.Services;

public class DubVideoJobManager : IDubVideoJobManager
{
    private readonly ConcurrentDictionary<string, DubVideoJobStatusDto> _jobs = new();

    public void CreateJob(string jobId, string userId)
    {
        CleanupOldJobs();
        _jobs[jobId] = new DubVideoJobStatusDto
        {
            JobId = jobId,
            UserId = userId,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow,
        };
    }

    public void SetProcessing(string jobId)
    {
        if (_jobs.TryGetValue(jobId, out var job)) job.Status = "Processing";
    }

    public void SetCompleted(string jobId, VideoAssetDto result)
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

    public DubVideoJobStatusDto? GetJob(string jobId, string userId)
    {
        return _jobs.TryGetValue(jobId, out var job) && job.UserId == userId ? job : null;
    }

    private void CleanupOldJobs()
    {
        var cutoff = DateTime.UtcNow.AddHours(-2);
        foreach (var job in _jobs.Where(item => item.Value.CreatedAt < cutoff).ToList())
        {
            _jobs.TryRemove(job.Key, out _);
        }
    }
}