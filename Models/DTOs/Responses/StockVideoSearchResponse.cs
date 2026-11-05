namespace PostFlow.Models.DTOs.Responses;

public class StockVideoSearchResponse
{
    public string Provider { get; set; } = string.Empty;
    public int Page { get; set; }
    public int PerPage { get; set; }
    public int TotalResults { get; set; }
    public bool FallbackUsed { get; set; }
    public List<StockVideoItemDto> Items { get; set; } = new();
}

public class StockVideoItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public string PreviewUrl { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public string? AuthorName { get; set; }
    public string? AuthorUrl { get; set; }
    public int DurationSeconds { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string Orientation { get; set; } = "landscape";
}
