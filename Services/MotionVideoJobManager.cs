using System.Collections.Concurrent;
using PostFlow.Models.DTOs.Responses;

namespace PostFlow.Services;

public class MotionVideoJobManager
{
    private readonly ConcurrentDictionary<string, MotionVideoJobStatusDto> _jobs = new();

    public string Create(string userId, int totalScenes)
    {
        var jobId = Guid.NewGuid().ToString();
        _jobs[jobId] = new MotionVideoJobStatusDto
        {
            JobId = jobId,
            UserId = userId,
            TotalScenes = totalScenes,
            Status = "Pending"
        };
        return jobId;
    }

    public MotionVideoJobStatusDto? Get(string jobId, string userId) =>
        _jobs.TryGetValue(jobId, out var job) && job.UserId == userId ? job : null;

    public void Processing(string jobId) { if (_jobs.TryGetValue(jobId, out var job)) job.Status = "Processing"; }
    public void Progress(string jobId, int scene, int total) { if (_jobs.TryGetValue(jobId, out var job)) { job.CurrentScene = scene; job.TotalScenes = total; job.Progress = total == 0 ? 0 : (int)Math.Round(scene * 100d / total); } }
    public void Completed(string jobId, string videoId, string videoUrl) { if (_jobs.TryGetValue(jobId, out var job)) { job.Status = "Completed"; job.Progress = 100; job.VideoId = videoId; job.VideoUrl = videoUrl; job.CompletedAt = DateTime.UtcNow; } }
    public void Failed(string jobId, string error) { if (_jobs.TryGetValue(jobId, out var job)) { job.Status = "Failed"; job.ErrorMessage = error; job.CompletedAt = DateTime.UtcNow; } }
}
