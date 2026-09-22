using System;
using System.Collections.Generic;
using PostFlow.Models;

namespace PostFlow.Models.DTOs.Responses
{
    public class VideoAssetDto
    {
        public string Id { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string? ContentDNAId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new List<string>();
        public string LocalFilePath { get; set; } = string.Empty;
        public int DurationSeconds { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? VideoUrl { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public static class VideoAssetMappingExtensions
    {
        public static VideoAssetDto ToDto(this VideoAsset video)
        {
            return new VideoAssetDto
            {
                Id = video.Id,
                UserId = video.UserId,
                ContentDNAId = video.ContentDNAId,
                Title = video.Title,
                Description = video.Description,
                Tags = video.Tags ?? new List<string>(),
                LocalFilePath = video.LocalFilePath,
                DurationSeconds = video.DurationSeconds,
                Status = video.Status,
                VideoUrl = video.LocalFilePath,
                CreatedAt = video.CreatedAt
            };
        }
    }
}
