using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PostFlow.Configuration;
using PostFlow.Data;
using PostFlow.Exceptions;
using PostFlow.Models;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Models.DTOs.Responses;
using PostFlow.Services.Interfaces;

namespace PostFlow.Services
{
    public class TrendService : ITrendService
    {
        private readonly AppDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _memoryCache;
        private readonly AISettings _aiSettings;
        private readonly PlatformSettings _platformSettings;
        private readonly ILogger<TrendService> _logger;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public TrendService(
            AppDbContext context,
            IHttpClientFactory httpClientFactory,
            IMemoryCache memoryCache,
            IOptions<AISettings> aiSettings,
            IOptions<PlatformSettings> platformSettings,
            ILogger<TrendService> logger)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _memoryCache = memoryCache;
            _aiSettings = aiSettings.Value;
            _platformSettings = platformSettings.Value;
            _logger = logger;
        }

        public async Task<IEnumerable<TrendTopicDto>> GetTrendingTopicsAsync(string? platform = null, string? category = null)
        {
            var cacheKey = $"trends_platform_feed_{platform ?? "all"}_{category ?? "all"}";
            if (_memoryCache.TryGetValue(cacheKey, out List<TrendTopicDto>? cachedTrends) && cachedTrends != null && cachedTrends.Count > 0)
            {
                return cachedTrends;
            }

            // 1. Nếu tìm kiếm trên YouTube hoặc Tất cả, thử lấy dữ liệu video thật từ YouTube Data API
            if (string.IsNullOrWhiteSpace(platform) || platform == "all" || platform.Equals("youtube", StringComparison.OrdinalIgnoreCase))
            {
                var ytApiKey = !string.IsNullOrWhiteSpace(_platformSettings?.YouTube?.ApiKey)
                    ? _platformSettings.YouTube.ApiKey
                    : Environment.GetEnvironmentVariable("YOUTUBE_API_KEY");

                if (!string.IsNullOrWhiteSpace(ytApiKey))
                {
                    try
                    {
                        var ytVideos = await FetchRealTrendingFromYouTubeApiAsync(ytApiKey, category);
                        if (ytVideos != null && ytVideos.Count > 0)
                        {
                            _memoryCache.Set(cacheKey, ytVideos, TimeSpan.FromMinutes(15));
                            return ytVideos;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Lỗi khi lấy video thật từ YouTube Data API: {Msg}", ex.Message);
                    }
                }
            }

            // 2. Thu thập và tổng hợp dữ liệu video xu hướng thực tế qua AI Tracker (OpenRouter)
            var openRouterKey = !string.IsNullOrWhiteSpace(_aiSettings?.OpenRouter?.ApiKey)
                ? _aiSettings.OpenRouter.ApiKey
                : Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");

            if (!string.IsNullOrWhiteSpace(openRouterKey))
            {
                try
                {
                    var aiTrends = await FetchTrendingFromOpenRouterAsync(openRouterKey, platform, category);
                    if (aiTrends != null && aiTrends.Count > 0)
                    {
                        _memoryCache.Set(cacheKey, aiTrends, TimeSpan.FromMinutes(15));
                        return aiTrends;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "OpenRouter Fetch Trends thất bại: {Msg}", ex.Message);
                }
            }

            // 3. Thu thập qua Google Gemini
            var geminiKey = !string.IsNullOrWhiteSpace(_aiSettings?.GeminiApiKey)
                ? _aiSettings.GeminiApiKey
                : Environment.GetEnvironmentVariable("GEMINI_API_KEY");

            if (!string.IsNullOrWhiteSpace(geminiKey))
            {
                try
                {
                    var geminiTrends = await FetchTrendingFromGeminiAsync(geminiKey, platform, category);
                    if (geminiTrends != null && geminiTrends.Count > 0)
                    {
                        _memoryCache.Set(cacheKey, geminiTrends, TimeSpan.FromMinutes(15));
                        return geminiTrends;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Gemini Fetch Trends thất bại: {Msg}", ex.Message);
                }
            }

            return new List<TrendTopicDto>();
        }

        public async Task<TrendAnalysisResponse> AnalyzeTrendAsync(string userId, AnalyzeTrendRequest request)
        {
            var openRouterKey = !string.IsNullOrWhiteSpace(_aiSettings?.OpenRouter?.ApiKey)
                ? _aiSettings.OpenRouter.ApiKey
                : Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");

            if (!string.IsNullOrWhiteSpace(openRouterKey))
            {
                try
                {
                    var result = await CallOpenRouterForAnalysisAsync(openRouterKey, request);
                    if (result != null)
                    {
                        await LogApiUsageAsync(userId, "OpenRouter", 1, 0.003m);
                        return result;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "OpenRouter Trend Analysis lỗi: {Msg}", ex.Message);
                }
            }

            var geminiKey = !string.IsNullOrWhiteSpace(_aiSettings?.GeminiApiKey)
                ? _aiSettings.GeminiApiKey
                : Environment.GetEnvironmentVariable("GEMINI_API_KEY");

            if (!string.IsNullOrWhiteSpace(geminiKey))
            {
                try
                {
                    var result = await CallGeminiForAnalysisAsync(geminiKey, request);
                    if (result != null)
                    {
                        await LogApiUsageAsync(userId, "Gemini", 1, 0.005m);
                        return result;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Gemini Trend Analysis lỗi: {Msg}", ex.Message);
                }
            }

            throw new BadRequestException("Không thể phân tích video xu hướng lúc này do dịch vụ AI tạm thời gián đoạn. Vui lòng thử lại sau.");
        }

        /// <summary>
        /// Thu thập video thịnh hành thật trực tiếp từ YouTube Data API v3
        /// </summary>
        private async Task<List<TrendTopicDto>?> FetchRealTrendingFromYouTubeApiAsync(string apiKey, string? category)
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);

            var categoryIdParam = string.Empty;
            if (!string.IsNullOrWhiteSpace(category) && category != "all")
            {
                var categoryId = category.ToLowerInvariant() switch
                {
                    "tech" => "28", // Science & Technology
                    "entertainment" => "24", // Entertainment
                    "education" => "27", // Education
                    "lifestyle" => "22", // People & Blogs
                    "business" => "25", // News & Politics
                    _ => string.Empty
                };

                if (!string.IsNullOrEmpty(categoryId))
                {
                    categoryIdParam = $"&videoCategoryId={categoryId}";
                }
            }

            var url = $"https://www.googleapis.com/youtube/v3/videos?part=snippet,contentDetails,statistics&chart=mostPopular&regionCode=VN&maxResults=8{categoryIdParam}&key={apiKey}";

            using var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;

            var responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);

            if (!doc.RootElement.TryGetProperty("items", out var items) || items.GetArrayLength() == 0) return null;

            var results = new List<TrendTopicDto>();

            foreach (var item in items.EnumerateArray())
            {
                var videoId = item.GetProperty("id").GetString() ?? string.Empty;
                if (string.IsNullOrEmpty(videoId)) continue;

                var snippet = item.GetProperty("snippet");
                var title = snippet.GetProperty("title").GetString() ?? "Video thịnh hành";
                var channelTitle = snippet.GetProperty("channelTitle").GetString() ?? string.Empty;
                var description = snippet.GetProperty("description").GetString() ?? string.Empty;

                var thumbnailUrl = string.Empty;
                if (snippet.TryGetProperty("thumbnails", out var thumbs))
                {
                    if (thumbs.TryGetProperty("high", out var highThumb))
                    {
                        thumbnailUrl = highThumb.GetProperty("url").GetString() ?? string.Empty;
                    }
                    else if (thumbs.TryGetProperty("default", out var defThumb))
                    {
                        thumbnailUrl = defThumb.GetProperty("url").GetString() ?? string.Empty;
                    }
                }

                var tagsList = new List<string>();
                if (snippet.TryGetProperty("tags", out var tagsElem))
                {
                    foreach (var tag in tagsElem.EnumerateArray().Take(4))
                    {
                        var t = tag.GetString();
                        if (!string.IsNullOrWhiteSpace(t)) tagsList.Add($"#{t.Replace(" ", "")}");
                    }
                }
                var publishedAt = DateTime.MinValue;
                if (snippet.TryGetProperty("publishedAt", out var publishedAtElement))
                {
                    DateTime.TryParse(publishedAtElement.GetString(), out publishedAt);
                }

                // Statistics
                var viewsCount = string.Empty;
                var likesCount = string.Empty;
                if (item.TryGetProperty("statistics", out var stats))
                {
                    if (stats.TryGetProperty("viewCount", out var vCount) && long.TryParse(vCount.GetString(), out var views))
                    {
                        viewsCount = views >= 1_000_000 ? $"{(views / 1_000_000.0):0.#}M views" : $"{(views / 1_000.0):0.#}K views";
                    }
                    if (stats.TryGetProperty("likeCount", out var lCount) && long.TryParse(lCount.GetString(), out var likes))
                    {
                        likesCount = likes >= 1_000_000 ? $"{(likes / 1_000_000.0):0.#}M likes" : $"{(likes / 1_000.0):0.#}K likes";
                    }
                }

                // Duration
                var duration = string.Empty;
                if (item.TryGetProperty("contentDetails", out var contentDetails) && contentDetails.TryGetProperty("duration", out var durElem))
                {
                    var durStr = durElem.GetString() ?? string.Empty;
                    duration = FormatIsoDuration(durStr);
                }

                results.Add(new TrendTopicDto
                {
                    Id = $"yt-{videoId}",
                    Title = title,
                    Platform = "youtube",
                    Category = category ?? "tech",
                    VideoUrl = $"https://www.youtube.com/watch?v={videoId}",
                    EmbedUrl = $"https://www.youtube.com/embed/{videoId}?autoplay=1",
                    AuthorName = channelTitle,
                    AuthorAvatarUrl = string.Empty,
                    ViewsCount = viewsCount,
                    LikesCount = likesCount,
                    Duration = duration,
                    ViralityScore = 0,
                    GrowthRate = string.Empty,
                    SearchVolume = viewsCount.Replace(" views", ""),
                    Hashtags = tagsList,
                    Description = description.Length > 150 ? description.Substring(0, 150) + "..." : description,
                    SampleHook = string.Empty,
                    ThumbnailUrl = thumbnailUrl,
                    TrendingSince = publishedAt
                });
            }

            return results;
        }

        private async Task<List<TrendTopicDto>?> FetchTrendingFromOpenRouterAsync(string apiKey, string? platform, string? category)
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(60);

            var url = !string.IsNullOrWhiteSpace(_aiSettings?.OpenRouter?.BaseUrl)
                ? _aiSettings.OpenRouter.BaseUrl
                : "https://openrouter.ai/api/v1/chat/completions";

            var promptText = BuildTrendingFeedPrompt(platform, category);

            var requestBody = new
            {
                model = "openrouter/free",
                messages = new object[]
                {
                    new { role = "system", content = "Bạn là AI Research Engine chuyên quét các video xu hướng (Viral Trends) thực tế trên mạng xã hội TikTok, YouTube Shorts, Reels. Luôn trả về đúng schema JSON Array với các video thực tế và đường link nhúng iframe hợp lệ." },
                    new { role = "user", content = promptText }
                },
                temperature = 0.7
            };

            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, url);
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            requestMessage.Headers.Add("HTTP-Referer", "https://postflow.io");
            requestMessage.Headers.Add("X-Title", "PostFlow Real Trend Discovery");
            requestMessage.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            using var response = await client.SendAsync(requestMessage);
            if (!response.IsSuccessStatusCode) return null;

            var responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);

            if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0) return null;

            var contentText = choices[0].GetProperty("message").GetProperty("content").GetString();
            if (string.IsNullOrWhiteSpace(contentText)) return null;

            var cleanJson = ExtractJsonArray(contentText);
            var parsed = JsonSerializer.Deserialize<List<TrendTopicDto>>(cleanJson, JsonOptions);
            return NormalizeTrends(parsed, platform, category);
        }

        private async Task<List<TrendTopicDto>?> FetchTrendingFromGeminiAsync(string apiKey, string? platform, string? category)
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(60);

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={apiKey}";
            var promptText = BuildTrendingFeedPrompt(platform, category);

            var requestBody = new
            {
                contents = new[]
                {
                    new { parts = new[] { new { text = promptText } } }
                },
                generationConfig = new
                {
                    response_mime_type = "application/json",
                    temperature = 0.7
                }
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            using var response = await client.PostAsync(url, jsonContent);
            if (!response.IsSuccessStatusCode) return null;

            var responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);

            if (!doc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0) return null;

            var candidateText = candidates[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
            if (string.IsNullOrWhiteSpace(candidateText)) return null;

            var cleanJson = ExtractJsonArray(candidateText);
            var parsed = JsonSerializer.Deserialize<List<TrendTopicDto>>(cleanJson, JsonOptions);
            return NormalizeTrends(parsed, platform, category);
        }

        private static string BuildTrendingFeedPrompt(string? platform, string? category)
        {
            var platformContext = string.IsNullOrWhiteSpace(platform) || platform.Equals("all", StringComparison.OrdinalIgnoreCase)
                ? "YouTube Shorts, TikTok, Facebook Reels"
                : platform;

            var categoryContext = string.IsNullOrWhiteSpace(category) || category.Equals("all", StringComparison.OrdinalIgnoreCase)
                ? "nhiều lĩnh vực đa dạng (Công nghệ, Kinh doanh, Đời sống, Giải trí, Giáo dục)"
                : category switch
                {
                    "tech" => "Công nghệ & Số hóa",
                    "business" => "Kinh doanh & Khởi nghiệp",
                    "lifestyle" => "Đời sống & Năng suất",
                    "entertainment" => "Giải trí & Phim ảnh",
                    "education" => "Mẹo hay & Kỹ năng",
                    _ => category
                };

            return $@"Hãy đóng vai AI Research Tracker quét các video thịnh hành (Viral Trends) thực tế trên mạng xã hội {platformContext} thuộc lĩnh vực {categoryContext}.
                Hãy tìm và tạo danh sách 6 video đang thực sự thịnh hành để người dùng xem trực tiếp qua iframe nhúng (embed player).

                YÊU CẦU:
                - Bắt buộc trả về đúng định dạng JSON Array thuần túy:
                [
                {{
                    ""id"": ""slug-video-ngan"",
                    ""title"": ""Tiêu đề video thực tế đang viral"",
                    ""platform"": ""youtube"" (hoặc ""tiktok"", ""reels""),
                    ""category"": ""tech"" (hoặc ""business"", ""lifestyle"", ""entertainment"", ""education""),
                    ""videoUrl"": ""https://www.youtube.com/watch?v=VIDEO_ID"" hoặc link TikTok gốc,
                    ""embedUrl"": ""https://www.youtube.com/embed/VIDEO_ID?autoplay=1"" hoặc link nhúng iframe,
                    ""authorName"": ""@TenKenhNguoiTao"",
                    ""authorAvatarUrl"": ""https://images.unsplash.com/photo-1535713875002-d1d0cf377fde?w=100&auto=format&fit=crop"",
                    ""viewsCount"": ""2.5M views"",
                    ""likesCount"": ""150K likes"",
                    ""duration"": ""0:45"",
                    ""viralityScore"": 96,
                    ""growthRate"": ""+220%"",
                    ""searchVolume"": ""3.2M"",
                    ""hashtags"": [""#Tag1"", ""#Tag2"", ""#Tag3""],
                    ""description"": ""Giải thích ngắn gọn 1-2 câu vì sao video này viral"",
                    ""sampleHook"": ""Câu hook mở đầu 3s đắt giá của video"",
                    ""thumbnailUrl"": ""https://images.unsplash.com/photo-1618005182384-a83a8bd57fbe?w=720&auto=format&fit=crop""
                }}
            ]";
        }

        private static List<TrendTopicDto>? NormalizeTrends(List<TrendTopicDto>? list, string? defaultPlatform, string? defaultCategory)
        {
            if (list == null) return null;
            var now = DateTime.UtcNow;

            for (int i = 0; i < list.Count; i++)
            {
                var item = list[i];
                if (string.IsNullOrWhiteSpace(item.Id))
                {
                    item.Id = $"trend-{Guid.NewGuid().ToString("N").Substring(0, 8)}";
                }

                if (string.IsNullOrWhiteSpace(item.Platform))
                {
                    item.Platform = (!string.IsNullOrWhiteSpace(defaultPlatform) && defaultPlatform != "all")
                        ? defaultPlatform
                        : "youtube";
                }

                if (string.IsNullOrWhiteSpace(item.Category))
                {
                    item.Category = (!string.IsNullOrWhiteSpace(defaultCategory) && defaultCategory != "all")
                        ? defaultCategory
                        : "tech";
                }

                if (item.TrendingSince == default)
                {
                    item.TrendingSince = now;
                }

                // Tự động chuẩn hóa embedUrl từ videoUrl nếu thiếu
                if (string.IsNullOrWhiteSpace(item.EmbedUrl) && !string.IsNullOrWhiteSpace(item.VideoUrl))
                {
                    if (item.VideoUrl.Contains("watch?v="))
                    {
                        var vId = item.VideoUrl.Split("watch?v=")[1].Split("&")[0];
                        item.EmbedUrl = $"https://www.youtube.com/embed/{vId}?autoplay=1";
                    }
                    else if (item.VideoUrl.Contains("youtu.be/"))
                    {
                        var vId = item.VideoUrl.Split("youtu.be/")[1].Split("?")[0];
                        item.EmbedUrl = $"https://www.youtube.com/embed/{vId}?autoplay=1";
                    }
                }
            }

            return list;
        }

        private async Task<TrendAnalysisResponse?> CallOpenRouterForAnalysisAsync(string apiKey, AnalyzeTrendRequest request)
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(60);

            var url = !string.IsNullOrWhiteSpace(_aiSettings?.OpenRouter?.BaseUrl)
                ? _aiSettings.OpenRouter.BaseUrl
                : "https://openrouter.ai/api/v1/chat/completions";

            var promptText = BuildTrendAnalysisPrompt(request);

            var requestBody = new
            {
                model = "openrouter/free",
                messages = new object[]
                {
                    new { role = "system", content = "Bạn là chuyên gia phân tích kịch bản video viral và giải mã cấu trúc giữ chân người xem. Luôn trả về đúng schema JSON thuần túy." },
                    new { role = "user", content = promptText }
                },
                temperature = 0.7
            };

            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, url);
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            requestMessage.Headers.Add("HTTP-Referer", "https://postflow.io");
            requestMessage.Headers.Add("X-Title", "PostFlow Video Analysis");
            requestMessage.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            using var response = await client.SendAsync(requestMessage);
            if (!response.IsSuccessStatusCode) return null;

            var responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);

            if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0) return null;

            var contentText = choices[0].GetProperty("message").GetProperty("content").GetString();
            if (string.IsNullOrWhiteSpace(contentText)) return null;

            var cleanJson = ExtractJsonObject(contentText);
            var parsed = JsonSerializer.Deserialize<TrendAnalysisResponse>(cleanJson, JsonOptions);

            if (parsed != null)
            {
                parsed.Topic = request.Topic;
                parsed.Platform = request.Platform;
                parsed.ModelUsed = "openrouter/free";
                parsed.ProviderUsed = "OpenRouter AI";
            }

            return parsed;
        }

        private async Task<TrendAnalysisResponse?> CallGeminiForAnalysisAsync(string apiKey, AnalyzeTrendRequest request)
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(60);

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={apiKey}";
            var promptText = BuildTrendAnalysisPrompt(request);

            var requestBody = new
            {
                contents = new[]
                {
                    new { parts = new[] { new { text = promptText } } }
                },
                generationConfig = new
                {
                    response_mime_type = "application/json",
                    temperature = 0.7
                }
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            using var response = await client.PostAsync(url, jsonContent);
            if (!response.IsSuccessStatusCode) return null;

            var responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);

            if (!doc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0) return null;

            var candidateText = candidates[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
            if (string.IsNullOrWhiteSpace(candidateText)) return null;

            var cleanJson = ExtractJsonObject(candidateText);
            var parsed = JsonSerializer.Deserialize<TrendAnalysisResponse>(cleanJson, JsonOptions);

            if (parsed != null)
            {
                parsed.Topic = request.Topic;
                parsed.Platform = request.Platform;
                parsed.ModelUsed = "gemini-1.5-flash";
                parsed.ProviderUsed = "Google Gemini";
            }

            return parsed;
        }

        private static string BuildTrendAnalysisPrompt(AnalyzeTrendRequest request)
        {
            return $@"Phân tích kịch bản và cấu trúc viral của video thịnh hành:
                Tiêu đề video: {request.Topic}
                Nền tảng: {request.Platform}
                Danh mục: {request.Category ?? "Chung"}
                Hashtags: {(request.Hashtags != null ? string.Join(", ", request.Hashtags) : "Không có")}
                Ngữ cảnh / Mô tả: {request.AdditionalContext ?? "Không có"}

                Hãy giải mã vì sao video này đạt triệu views, tâm lý người xem, cấu trúc kịch bản và đưa ra 3 góc làm lại video tương tự nhưng sáng tạo và cuốn hút hơn.

                BẮT BUỘC TRẢ VỀ JSON THUẦN TÚY:
                {{
                ""viralityScore"": 96,
                ""whyTrendingReason"": ""Giải thích chi tiết 2-3 câu vì sao video này đang bùng nổ views (Hook 3s, nhịp độ dựng, tâm lý tò mò/FOMO)..."",
                ""targetAudience"": ""Mô tả tệp khán giả mục tiêu (độ tuổi, sở thích, hành vi)"",
                ""emotionalTriggers"": [
                    ""Tò mò & Bất ngờ"",
                    ""Khát khao phát triển"",
                    ""Tiết kiệm thời gian""
                ],
                ""angles"": [
                    {{
                    ""angleTitle"": ""Góc 1: Kể lại trải nghiệm thực tế (Storytelling)"",
                    ""targetHook"": ""Câu hook mở đầu 3s giữ chân người xem"",
                    ""contentBrief"": ""Quy trình 3 bước triển khai nội dung trong 45s""
                    }},
                    {{
                    ""angleTitle"": ""Góc 2: Phản biện / Lật ngược góc nhìn thông thường"",
                    ""targetHook"": ""Câu hook thách thức quan điểm cũ"",
                    ""contentBrief"": ""Đưa ra bằng chứng và giải pháp đột phá""
                    }},
                    {{
                    ""angleTitle"": ""Góc 3: Hướng dẫn thực hành nhanh (Tutorial)"",
                    ""targetHook"": ""Câu hook hành động tạo kết quả tức thì"",
                    ""contentBrief"": ""Các bước thực hiện nhanh gọn dễ làm theo""
                    }}
                ],
                ""recommendedHashtags"": [""#Hashtag1"", ""#Hashtag2"", ""#Hashtag3"", ""#Hashtag4""],
                ""suggestedCta"": ""Lời kêu gọi hành động cuối video tăng follow và comment"",
                ""generatedVideoIdea"": ""Đoạn prompt hoàn chỉnh để sinh video mới tương tự""
            }}";
        }

        private static string FormatIsoDuration(string iso)
        {
            if (string.IsNullOrWhiteSpace(iso)) return "0:45";
            try
            {
                var ts = System.Xml.XmlConvert.ToTimeSpan(iso);
                return $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}";
            }
            catch
            {
                return "0:45";
            }
        }

        private static string ExtractJsonObject(string rawText)
        {
            if (string.IsNullOrWhiteSpace(rawText)) return string.Empty;
            var text = rawText.Trim();
            var first = text.IndexOf('{');
            var last = text.LastIndexOf('}');
            if (first >= 0 && last > first)
            {
                return text.Substring(first, last - first + 1);
            }
            return text;
        }

        private static string ExtractJsonArray(string rawText)
        {
            if (string.IsNullOrWhiteSpace(rawText)) return "[]";
            var text = rawText.Trim();
            var first = text.IndexOf('[');
            var last = text.LastIndexOf(']');
            if (first >= 0 && last > first)
            {
                return text.Substring(first, last - first + 1);
            }
            return "[]";
        }

        private async Task LogApiUsageAsync(string userId, string provider, int count, decimal cost)
        {
            var today = DateTime.UtcNow.Date;
            var log = await _context.ApiUsageLogs
                .FirstOrDefaultAsync(l => l.UserId == userId && l.Provider == provider && l.Date == today);

            if (log == null)
            {
                log = new ApiUsageLog
                {
                    UserId = userId,
                    Provider = provider,
                    CallCount = count,
                    CostEstimate = cost,
                    Date = today
                };
                _context.ApiUsageLogs.Add(log);
            }
            else
            {
                log.CallCount += count;
                log.CostEstimate += cost;
            }

            await _context.SaveChangesAsync();
        }
    }
}
