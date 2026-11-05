using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using PostFlow.Configuration;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Models.DTOs.Responses;

namespace PostFlow.Services.StockVideo;

public class PexelsStockVideoProvider : IStockVideoProvider
{
    private readonly HttpClient _httpClient;
    private readonly StockVideoSettings _settings;
    private readonly IMemoryCache _cache;

    public PexelsStockVideoProvider(HttpClient httpClient, IOptions<StockVideoSettings> settings, IMemoryCache cache)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _cache = cache;
    }

    public string Name => "pexels";

    public async Task<StockVideoSearchResponse> SearchAsync(
        SearchStockVideoRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.PexelsApiKey))
        {
            throw new InvalidOperationException("Chưa cấu hình StockVideo:PexelsApiKey.");
        }

        var perPage = Math.Min(request.PerPage, _settings.MaxSearchResults);
        var cacheKey = $"pexels:{request.Query.Trim().ToLowerInvariant()}:{request.Page}:{perPage}:{request.Orientation}";
        if (_cache.TryGetValue(cacheKey, out StockVideoSearchResponse? cached) && cached != null)
            return cached;

        var url = $"https://api.pexels.com/videos/search?query={Uri.EscapeDataString(request.Query.Trim())}" +
                  $"&page={request.Page}&per_page={perPage}";
        if (!string.IsNullOrWhiteSpace(request.Orientation))
        {
            url += $"&orientation={Uri.EscapeDataString(request.Orientation)}";
        }

        string payload;
        var attempts = Math.Max(0, _settings.SearchRetryCount) + 1;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _settings.SearchTimeoutSeconds)));
                using var message = new HttpRequestMessage(HttpMethod.Get, url);
                message.Headers.Authorization = new AuthenticationHeaderValue(_settings.PexelsApiKey);
                using var response = await _httpClient.SendAsync(message, timeoutCts.Token);
                payload = await response.Content.ReadAsStringAsync(timeoutCts.Token);
                response.EnsureSuccessStatusCode();
                break;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && attempt < attempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500 * attempt), cancellationToken);
            }
            catch (HttpRequestException ex) when (
                attempt < attempts &&
                ex.StatusCode != System.Net.HttpStatusCode.TooManyRequests &&
                (ex.StatusCode == null || (int)ex.StatusCode >= 500))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500 * attempt), cancellationToken);
            }
        }

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        var result = new StockVideoSearchResponse
        {
            Provider = Name,
            Page = root.TryGetProperty("page", out var page) ? page.GetInt32() : request.Page,
            PerPage = root.TryGetProperty("per_page", out var responsePerPage) ? responsePerPage.GetInt32() : perPage,
            TotalResults = root.TryGetProperty("total_results", out var total) ? total.GetInt32() : 0
        };

        if (!root.TryGetProperty("videos", out var videos)) return result;
        foreach (var video in videos.EnumerateArray())
        {
            var files = video.TryGetProperty("video_files", out var filesElement)
                ? filesElement.EnumerateArray().ToList()
                : new List<JsonElement>();
            var file = files
                .Where(x => x.TryGetProperty("file_type", out var type) && type.GetString() == "video/mp4")
                .OrderBy(x => Math.Abs((x.TryGetProperty("width", out var width) ? width.GetInt32() : 720) - 720))
                .FirstOrDefault();
            if (file.ValueKind == JsonValueKind.Undefined || !file.TryGetProperty("link", out var link)) continue;

            var widthValue = file.TryGetProperty("width", out var fileWidth) ? fileWidth.GetInt32() : 0;
            var heightValue = file.TryGetProperty("height", out var fileHeight) ? fileHeight.GetInt32() : 0;
            result.Items.Add(new StockVideoItemDto
            {
                Id = video.GetProperty("id").GetInt32().ToString(),
                Provider = Name,
                Title = request.Query,
                ThumbnailUrl = video.TryGetProperty("image", out var image) ? image.GetString() ?? string.Empty : string.Empty,
                PreviewUrl = link.GetString() ?? string.Empty,
                DownloadUrl = link.GetString() ?? string.Empty,
                SourceUrl = video.TryGetProperty("url", out var source) ? source.GetString() ?? string.Empty : string.Empty,
                AuthorName = video.TryGetProperty("user", out var user) && user.TryGetProperty("name", out var name) ? name.GetString() : null,
                AuthorUrl = video.TryGetProperty("user", out var author) && author.TryGetProperty("url", out var authorUrl) ? authorUrl.GetString() : null,
                DurationSeconds = video.TryGetProperty("duration", out var duration) ? duration.GetInt32() : 0,
                Width = widthValue,
                Height = heightValue,
                Orientation = heightValue >= widthValue ? "portrait" : "landscape"
            });
        }

        _cache.Set(cacheKey, result, TimeSpan.FromMinutes(Math.Max(1, _settings.SearchCacheMinutes)));
        return result;
    }
}
