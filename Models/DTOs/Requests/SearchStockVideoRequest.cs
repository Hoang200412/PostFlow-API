using System.ComponentModel.DataAnnotations;

namespace PostFlow.Models.DTOs.Requests;

public class SearchStockVideoRequest
{
    [Required, MaxLength(100)]
    public string Query { get; set; } = string.Empty;

    [Range(1, 100)]
    public int Page { get; set; } = 1;

    [Range(1, 50)]
    public int PerPage { get; set; } = 12;

    public string? Orientation { get; set; }
}
