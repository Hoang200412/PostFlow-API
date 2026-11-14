using System;
using System.Collections.Generic;
using PostFlow.Models.DTOs.Requests;

namespace PostFlow.Models.DTOs.Responses
{
    public class ExplainerResearchResponse
    {
        public string Topic { get; set; } = string.Empty;
        public string CoreDefinition { get; set; } = string.Empty;
        public string TargetAudience { get; set; } = string.Empty;
        public List<string> KeyFacts { get; set; } = new();
        public List<string> CommonMisconceptions { get; set; } = new();
        public List<string> RecommendedHooks { get; set; } = new();
        public List<ExplainerChapterDto> SuggestedChapters { get; set; } = new();
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }

    public class ExplainerStoryboardResponse
    {
        public string Topic { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string AspectRatio { get; set; } = "16:9";
        public List<ExplainerSceneDto> Scenes { get; set; } = new();
        public int TotalEstimatedSeconds { get; set; }
    }

    public class ExplainerJobCreatedResponse
    {
        public string JobId { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending";
        public string Message { get; set; } = string.Empty;
    }

    public class ExplainerJobStatusResponse
    {
        public string JobId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string JobType { get; set; } = "Render"; // "Research", "Storyboard", "Render"
        public string Status { get; set; } = "Pending"; // "Pending", "Processing", "Completed", "Failed"
        public int ProgressPercent { get; set; }
        public string CurrentStep { get; set; } = string.Empty;
        public ExplainerResearchResponse? ResearchData { get; set; }
        public ExplainerStoryboardResponse? StoryboardData { get; set; }
        public string? VideoAssetId { get; set; }
        public string? VideoDownloadUrl { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
    }
}

