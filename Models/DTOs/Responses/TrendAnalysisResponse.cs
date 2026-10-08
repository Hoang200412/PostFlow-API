using System.Collections.Generic;

namespace PostFlow.Models.DTOs.Responses
{
    public class TrendAngleSuggestion
    {
        public string AngleTitle { get; set; } = string.Empty;
        public string TargetHook { get; set; } = string.Empty;
        public string ContentBrief { get; set; } = string.Empty;
    }

    public class TrendAnalysisResponse
    {
        public string Topic { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty;
        public int ViralityScore { get; set; } = 92;
        public string WhyTrendingReason { get; set; } = string.Empty;
        public string TargetAudience { get; set; } = string.Empty;
        public List<string> EmotionalTriggers { get; set; } = new();
        public List<TrendAngleSuggestion> Angles { get; set; } = new();
        public List<string> RecommendedHashtags { get; set; } = new();
        public string SuggestedCta { get; set; } = string.Empty;
        public string GeneratedVideoIdea { get; set; } = string.Empty;
        public string ModelUsed { get; set; } = string.Empty;
        public string ProviderUsed { get; set; } = string.Empty;
    }
}
