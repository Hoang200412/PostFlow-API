using System.Collections.Generic;

namespace PostFlow.Models.DTOs.Requests;

public class UpdatePostRequest
{
    public string? Title { get; set; }
    public string? Content { get; set; }
    public string? ShortDescription { get; set; }
    public string? Hook { get; set; }
    public string? CallToAction { get; set; }
    public List<string>? Hashtags { get; set; }
    public List<string>? Keywords { get; set; }
    public string? ImageUrl { get; set; }
    public string? Status { get; set; }
}
