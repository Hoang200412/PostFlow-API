using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace PostFlow.Models.DTOs.Requests;

public class MediaLibraryUploadRequest
{
    [Required] public IFormFile File { get; set; } = null!;
    [Required, RegularExpression("^(video|audio)$")] public string Type { get; set; } = "video";
    [Required, MaxLength(200)] public string Title { get; set; } = string.Empty;
    [MaxLength(1000)] public string? Description { get; set; }
    public string? Tags { get; set; }
    [MaxLength(2000)] public string? SourceUrl { get; set; }
    [MaxLength(200)] public string? AuthorName { get; set; }
    [MaxLength(2000)] public string? AuthorUrl { get; set; }
    [MaxLength(200)] public string? License { get; set; }
}
