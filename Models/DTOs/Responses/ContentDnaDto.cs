using System;
using PostFlow.Models;

namespace PostFlow.Models.DTOs.Responses
{
    public class ContentDnaDto
    {
        public string Id { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string BrandName { get; set; } = string.Empty;
        public string VoiceProfile { get; set; } = string.Empty;
        public string AvatarAssetPath { get; set; } = string.Empty;
        public string ScriptStructure { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public static class ContentDnaMappingExtensions
    {
        public static ContentDnaDto ToDto(this ContentDNA dna)
        {
            return new ContentDnaDto
            {
                Id = dna.Id,
                UserId = dna.UserId,
                BrandName = dna.BrandName,
                VoiceProfile = dna.VoiceProfile,
                AvatarAssetPath = dna.AvatarAssetPath,
                ScriptStructure = dna.ScriptStructure,
                CreatedAt = dna.CreatedAt
            };
        }
    }
}
