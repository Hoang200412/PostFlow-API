using System.ComponentModel.DataAnnotations;

namespace PostFlow.Models.DTOs.Requests;

public class MediaLibraryMetadataRequest
{
    [Required, MaxLength(200)] public string Title { get; set; } = string.Empty;
    [MaxLength(1000)] public string? Description { get; set; }
    public string? Tags { get; set; }
    [MaxLength(2000)] public string? SourceUrl { get; set; }
    [MaxLength(200)] public string? AuthorName { get; set; }
    [MaxLength(2000)] public string? AuthorUrl { get; set; }
    [MaxLength(200)] public string? License { get; set; }
    public bool IsActive { get; set; } = true;
}
