using System;
using System.Collections.Concurrent;
using System.Linq;
using PostFlow.Models.DTOs.Responses;

namespace PostFlow.Services
{
    public class ExplainerVideoJobManager
    {
        private readonly ConcurrentDictionary<string, ExplainerJobStatusResponse> _jobs = new();

        public string Create(string userId, string jobType = "Render")
        {
            CleanupOldJobs();
            var jobId = Guid.NewGuid().ToString("N");
            _jobs[jobId] = new ExplainerJobStatusResponse
            {
                JobId = jobId,
                UserId = userId,
                JobType = jobType,
                Status = "Pending",
                ProgressPercent = 5,
                CurrentStep = jobType switch
                {
                    "Research" => "Đã tiếp nhận yêu cầu nghiên cứu chủ đề",
                    "Storyboard" => "Đã tiếp nhận yêu cầu tạo kịch bản phân cảnh",
                    _ => "Đã tiếp nhận yêu cầu kết xuất video"
                },
                CreatedAt = DateTime.UtcNow
            };
            return jobId;
        }

        public ExplainerJobStatusResponse? Get(string jobId, string userId)
        {
            if (_jobs.TryGetValue(jobId, out var job) && (string.IsNullOrEmpty(userId) || job.UserId == userId))
            {
                return job;
            }
            return null;
        }

        public void UpdateProgress(string jobId, string status, int percent, string currentStep)
        {
            if (_jobs.TryGetValue(jobId, out var job))
            {
                job.Status = status;
                job.ProgressPercent = percent;
                job.CurrentStep = currentStep;
            }
        }

        public void CompleteResearch(string jobId, ExplainerResearchResponse data)
        {
            if (_jobs.TryGetValue(jobId, out var job))
            {
                job.Status = "Completed";
                job.ProgressPercent = 100;
                job.CurrentStep = "Hoàn tất nghiên cứu chủ đề";
                job.ResearchData = data;
                job.CompletedAt = DateTime.UtcNow;
            }
        }

        public void CompleteStoryboard(string jobId, ExplainerStoryboardResponse data)
        {
            if (_jobs.TryGetValue(jobId, out var job))
            {
                job.Status = "Completed";
                job.ProgressPercent = 100;
                job.CurrentStep = "Hoàn tất sinh kịch bản phân cảnh";
                job.StoryboardData = data;
                job.CompletedAt = DateTime.UtcNow;
            }
        }

        public void Completed(string jobId, string videoAssetId, string downloadUrl)
        {
            if (_jobs.TryGetValue(jobId, out var job))
            {
                job.Status = "Completed";
                job.ProgressPercent = 100;
                job.CurrentStep = "Hoàn thành xuất bản video";
                job.VideoAssetId = videoAssetId;
                job.VideoDownloadUrl = downloadUrl;
                job.CompletedAt = DateTime.UtcNow;
            }
        }

        public void Failed(string jobId, string errorMessage)
        {
            if (_jobs.TryGetValue(jobId, out var job))
            {
                job.Status = "Failed";
                job.ErrorMessage = errorMessage;
                job.CurrentStep = "Xử lý thất bại: " + errorMessage;
                job.CompletedAt = DateTime.UtcNow;
            }
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

