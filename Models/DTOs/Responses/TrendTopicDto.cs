using System;
using System.Collections.Generic;

namespace PostFlow.Models.DTOs.Responses
{
    public class TrendTopicDto
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Platform { get; set; } = "all"; // tiktok, youtube, reels, twitter
        public string Category { get; set; } = string.Empty; // tech, business, lifestyle, entertainment, education
        public string VideoUrl { get; set; } = string.Empty; // Direct URL to the real video on platform
        public string EmbedUrl { get; set; } = string.Empty; // Iframe embed URL (youtube.com/embed/..., tiktok.com/embed/...)
        public string AuthorName { get; set; } = string.Empty; // Channel / Creator handle
        public string AuthorAvatarUrl { get; set; } = string.Empty; // Creator profile image
        public string ViewsCount { get; set; } = "1.2M";
        public string LikesCount { get; set; } = "85K";
        public string Duration { get; set; } = "0:45";
        public int ViralityScore { get; set; } = 90; // 0 - 100
        public string GrowthRate { get; set; } = "+120%";
        public string SearchVolume { get; set; } = "1.2M";
        public List<string> Hashtags { get; set; } = new();
        public string Description { get; set; } = string.Empty;
        public string SampleHook { get; set; } = string.Empty;
        public string ThumbnailUrl { get; set; } = string.Empty;
        public DateTime TrendingSince { get; set; } = DateTime.UtcNow;
    }
}
