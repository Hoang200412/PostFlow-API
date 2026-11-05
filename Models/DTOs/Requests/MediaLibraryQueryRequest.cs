using System.ComponentModel.DataAnnotations;

namespace PostFlow.Models.DTOs.Requests;

public class MediaLibraryQueryRequest
{
    public string? Query { get; set; }
    public string? Type { get; set; } = "video";
    [Range(1, 100)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
    public bool ActiveOnly { get; set; } = true;
}
