using System;
using System.Collections.Generic;

namespace PostFlow.Models;

public class Post
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string Platform { get; set; } = "facebook";
    public string Prompt { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string ShortDescription { get; set; } = string.Empty;
    public string Hook { get; set; } = string.Empty;
    public string CallToAction { get; set; } = string.Empty;
    public List<string> Hashtags { get; set; } = new();
    public List<string> Keywords { get; set; } = new();
    public string ImageUrl { get; set; } = string.Empty;
    public string ImagePrompt { get; set; } = string.Empty;
    public string ImageStyle { get; set; } = "realistic";
    public string AspectRatio { get; set; } = "1:1";
    public string Status { get; set; } = "Draft";
    public DateTime? ScheduledAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public virtual User? User { get; set; }
}
