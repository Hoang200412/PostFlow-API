using System;
using System.Collections.Generic;
using PostFlow.Models;

namespace PostFlow.Models.DTOs.Responses;

public class PostDto
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
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
    public string ImageStyle { get; set; } = string.Empty;
    public string AspectRatio { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? ScheduledAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public static PostDto From(Post post) => new()
    {
        Id = post.Id,
        UserId = post.UserId,
        Platform = post.Platform,
        Prompt = post.Prompt,
        Title = post.Title,
        Content = post.Content,
        ShortDescription = post.ShortDescription,
        Hook = post.Hook,
        CallToAction = post.CallToAction,
        Hashtags = post.Hashtags ?? new List<string>(),
        Keywords = post.Keywords ?? new List<string>(),
        ImageUrl = post.ImageUrl,
        ImagePrompt = post.ImagePrompt,
        ImageStyle = post.ImageStyle,
        AspectRatio = post.AspectRatio,
        Status = post.Status,
        ScheduledAt = post.ScheduledAt,
        PublishedAt = post.PublishedAt,
        CreatedAt = post.CreatedAt
    };
}
