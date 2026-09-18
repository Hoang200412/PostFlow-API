using System;
using System.ComponentModel.DataAnnotations;

namespace PostFlow.Models.DTOs.Requests
{
    public class EnqueueUploadRequest
    {
        [Required(ErrorMessage = "UserId không được để trống.")]
        public string UserId { get; set; } = string.Empty;

        [Required(ErrorMessage = "VideoAssetId không được để trống.")]
        public string VideoAssetId { get; set; } = string.Empty;

        [Required(ErrorMessage = "SocialAccountId không được để trống.")]
        public string SocialAccountId { get; set; } = string.Empty;

        public DateTime? ScheduledAt { get; set; }
        public string? PlatformMetadataOverride { get; set; }
    }
}
