using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PostFlow.Configuration;
using PostFlow.Data;
using PostFlow.Exceptions;
using PostFlow.Models;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Models.DTOs.Responses;
using PostFlow.Services.Interfaces;

namespace PostFlow.Services;

public class PostService : IPostService
{
    private readonly AppDbContext _context;
    private readonly IImageGenerationService _imageGenerationService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AISettings _aiSettings;
    private readonly ILogger<PostService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public PostService(
        AppDbContext context,
        IImageGenerationService imageGenerationService,
        IHttpClientFactory httpClientFactory,
        IOptions<AISettings> aiSettings,
        ILogger<PostService> logger)
    {
        _context = context;
        _imageGenerationService = imageGenerationService;
        _httpClientFactory = httpClientFactory;
        _aiSettings = aiSettings.Value;
        _logger = logger;
    }

    public async Task<PostDto> GenerateAsync(string userId, GeneratePostRequest request)
    {
        var content = await GenerateContentAsync(request);
        var imagePrompt = $"{request.Prompt}, {content.Title}, {content.Hook}";
        var imageUrl = await _imageGenerationService.GenerateImageUrlAsync(
            imagePrompt,
            request.ImageStyle,
            request.AspectRatio,
            request.ColorTone,
            request.Mood,
            request.IncludeTextInImage,
            request.ReferenceImageUrl);

        var post = new Post
        {
            UserId = userId,
            Platform = request.Platform.Trim().ToLowerInvariant(),
            Prompt = request.Prompt.Trim(),
            Title = content.Title,
            Content = content.Content,
            ShortDescription = content.ShortDescription,
            Hook = content.Hook,
            CallToAction = content.CallToAction,
            Hashtags = content.Hashtags,
            Keywords = content.Keywords,
            ImageUrl = imageUrl,
            ImagePrompt = imagePrompt,
            ImageStyle = request.ImageStyle,
            AspectRatio = request.AspectRatio,
            Status = "Draft",
            CreatedAt = DateTime.UtcNow
        };

        _context.Posts.Add(post);
        await _context.SaveChangesAsync();
        return PostDto.From(post);
    }

    public async Task<IEnumerable<PostDto>> GetListAsync(string userId)
    {
        var posts = await _context.Posts
            .AsNoTracking()
            .Where(post => post.UserId == userId)
            .OrderByDescending(post => post.CreatedAt)
            .ToListAsync();
        return posts.Select(PostDto.From);
    }

    public async Task<PostDto> GetByIdAsync(string userId, string id)
    {
        var post = await FindOwnedPostAsync(userId, id);
        return PostDto.From(post);
    }

    public async Task<PostDto> UpdateAsync(string userId, string id, UpdatePostRequest request)
    {
        var post = await FindOwnedPostAsync(userId, id);
        post.Title = request.Title ?? post.Title;
        post.Content = request.Content ?? post.Content;
        post.ShortDescription = request.ShortDescription ?? post.ShortDescription;
        post.Hook = request.Hook ?? post.Hook;
        post.CallToAction = request.CallToAction ?? post.CallToAction;
        post.Hashtags = request.Hashtags ?? post.Hashtags;
        post.Keywords = request.Keywords ?? post.Keywords;
        post.ImageUrl = request.ImageUrl ?? post.ImageUrl;
        post.Status = request.Status ?? post.Status;
        post.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return PostDto.From(post);
    }

    public async Task DeleteAsync(string userId, string id)
    {
        var post = await FindOwnedPostAsync(userId, id);
        _context.Posts.Remove(post);
        await _context.SaveChangesAsync();
    }

    private async Task<Post> FindOwnedPostAsync(string userId, string id) =>
        await _context.Posts.FirstOrDefaultAsync(post => post.Id == id && post.UserId == userId)
        ?? throw new NotFoundException($"Không tìm thấy bài viết với ID {id}");

    private async Task<GeneratedPostContent> GenerateContentAsync(GeneratePostRequest request)
    {
        var apiKey = !string.IsNullOrWhiteSpace(_aiSettings.OpenRouter.ApiKey)
            ? _aiSettings.OpenRouter.ApiKey
            : Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new BadRequestException("Chưa cấu hình OpenRouter API key để sinh nội dung.");
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(45);
            var body = new
            {
                model = _aiSettings.OpenRouter.Models.FirstOrDefault() ?? "openrouter/free",
                messages = new object[]
                {
                    new { role = "system", content = "Bạn là chuyên gia viết nội dung mạng xã hội. Chỉ trả về JSON hợp lệ." },
                    new { role = "user", content = BuildContentPrompt(request) }
                },
                temperature = 0.7
            };

            using var message = new HttpRequestMessage(HttpMethod.Post, _aiSettings.OpenRouter.BaseUrl);
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            message.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

            using var response = await client.SendAsync(message);
            if (!response.IsSuccessStatusCode)
            {
                throw new BadRequestException("Dịch vụ sinh nội dung không phản hồi thành công.");
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var rawContent = document.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();
            var json = ExtractJson(rawContent ?? string.Empty);
            return JsonSerializer.Deserialize<GeneratedPostContent>(json, JsonOptions)
                ?? throw new BadRequestException("Dịch vụ sinh nội dung trả về dữ liệu không hợp lệ.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không thể sinh nội dung bài viết.");
            throw new BadRequestException("Không thể sinh nội dung bài viết từ dịch vụ AI.");
        }
    }

        private static string BuildContentPrompt(GeneratePostRequest request) => $@"Chủ đề: {request.Prompt}
            Nền tảng: {request.Platform}
            Tone: {request.Tone}
            Hãy tạo nội dung bài viết bằng tiếng Việt với JSON schema:
            {{
                ""title"": ""tiêu đề"",
                ""content"": ""nội dung đầy đủ"",
                ""shortDescription"": ""mô tả ngắn"",
                ""hook"": ""câu mở đầu"",
                ""callToAction"": ""lời kêu gọi hành động"",
                ""hashtags"": [""#tag1"", ""#tag2""],
                ""keywords"": [""từ khóa 1"", ""từ khóa 2""]
            }}
            Không thêm markdown hoặc văn bản ngoài JSON.";

    private static string ExtractJson(string text)
    {
        var first = text.IndexOf('{');
        var last = text.LastIndexOf('}');
        return first >= 0 && last > first ? text.Substring(first, last - first + 1) : text.Trim();
    }

    private sealed class GeneratedPostContent
    {
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string ShortDescription { get; set; } = string.Empty;
        public string Hook { get; set; } = string.Empty;
        public string CallToAction { get; set; } = string.Empty;
        public List<string> Hashtags { get; set; } = new();
        public List<string> Keywords { get; set; } = new();
    }
}
