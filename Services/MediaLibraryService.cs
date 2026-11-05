using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PostFlow.Configuration;
using PostFlow.Data;
using PostFlow.Exceptions;
using PostFlow.Models;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Models.DTOs.Responses;
using PostFlow.Services.Interfaces;

namespace PostFlow.Services;

public class MediaLibraryService : IMediaLibraryService
{
    private readonly AppDbContext _context;
    private readonly IStorageService _storage;
    private readonly StockVideoSettings _settings;

    public MediaLibraryService(AppDbContext context, IStorageService storage, IOptions<StockVideoSettings> settings)
    {
        _context = context;
        _storage = storage;
        _settings = settings.Value;
    }

    public async Task<PagedResult<MediaLibraryAssetDto>> GetAsync(MediaLibraryQueryRequest request, CancellationToken cancellationToken = default)
    {
        var query = _context.MediaLibraryAssets.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Type)) query = query.Where(x => x.Type == request.Type);
        if (request.ActiveOnly) query = query.Where(x => x.IsActive);
        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            var term = request.Query.Trim().ToLower();
            query = query.Where(x => x.Title.ToLower().Contains(term) || x.Description.ToLower().Contains(term));
        }
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(cancellationToken);
        return new PagedResult<MediaLibraryAssetDto>
        {
            Items = await ToDtosAsync(items),
            Total = total,
            Page = request.Page,
            Limit = request.PageSize
        };
    }

    public async Task<MediaLibraryAssetDto> UploadAsync(string adminId, MediaLibraryUploadRequest request, CancellationToken cancellationToken = default)
    {
        var file = request.File;
        var allowed = request.Type == "video"
            ? new[] { "video/mp4", "video/webm", "video/quicktime" }
            : new[] { "audio/mpeg", "audio/wav", "audio/x-wav", "audio/mp4", "audio/aac" };
        if (file.Length == 0 || !allowed.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
            throw new BadRequestException("Định dạng media không được hỗ trợ.");
        var maxBytes = (request.Type == "video" ? _settings.MaxMediaLibraryVideoSizeMb : _settings.MaxMusicSizeMb) * 1024L * 1024L;
        if (file.Length > maxBytes) throw new BadRequestException("File vượt quá dung lượng cho phép.");
        var ext = Path.GetExtension(file.FileName);
        await using var stream = file.OpenReadStream();
        var objectKey = await _storage.SaveFileAsync(stream, $"media-library/{request.Type}s/{DateTime.UtcNow:yyyy/MM}", $"{Guid.NewGuid():N}{ext}", file.ContentType);
        var asset = new MediaLibraryAsset
        {
            Type = request.Type, Title = request.Title.Trim(), Description = request.Description ?? string.Empty,
            Tags = ParseTags(request.Tags), ObjectKey = objectKey, MimeType = file.ContentType,
            FileSizeBytes = file.Length, Provider = "Internal", SourceUrl = request.SourceUrl,
            AuthorName = request.AuthorName, AuthorUrl = request.AuthorUrl, License = request.License,
            CreatedBy = adminId
        };
        if (request.Type == "video")
        {
            var temp = await _storage.DownloadToTempFileAsync(objectKey);
            try { await ReadMediaInfoAsync(temp, asset); } finally { TryDelete(temp); }
        }
        _context.MediaLibraryAssets.Add(asset);
        await _context.SaveChangesAsync(cancellationToken);
        return (await ToDtosAsync(new[] { asset })).Single();
    }

    public async Task<MediaLibraryAssetDto> UpdateAsync(string id, MediaLibraryMetadataRequest request)
    {
        var asset = await _context.MediaLibraryAssets.FindAsync(id) ?? throw new NotFoundException("Không tìm thấy media.");
        asset.Title = request.Title.Trim(); asset.Description = request.Description ?? string.Empty;
        asset.Tags = ParseTags(request.Tags); asset.SourceUrl = request.SourceUrl; asset.AuthorName = request.AuthorName;
        asset.AuthorUrl = request.AuthorUrl; asset.License = request.License; asset.IsActive = request.IsActive;
        asset.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return (await ToDtosAsync(new[] { asset })).Single();
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var asset = await _context.MediaLibraryAssets.FindAsync(id);
        if (asset == null) return false;
        await _storage.DeleteFileAsync(asset.ObjectKey);
        if (!string.IsNullOrWhiteSpace(asset.ThumbnailObjectKey)) await _storage.DeleteFileAsync(asset.ThumbnailObjectKey);
        _context.MediaLibraryAssets.Remove(asset);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<StockVideoSearchResponse> SearchVideosAsync(SearchStockVideoRequest request, CancellationToken cancellationToken = default)
    {
        var query = _context.MediaLibraryAssets.AsNoTracking().Where(x => x.Type == "video" && x.IsActive);
        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            var term = request.Query.Trim().ToLower();
            query = query.Where(x => x.Title.ToLower().Contains(term) || x.Description.ToLower().Contains(term));
        }
        var assets = await query.OrderByDescending(x => x.CreatedAt).Skip((request.Page - 1) * request.PerPage).Take(request.PerPage).ToListAsync(cancellationToken);
        var result = new StockVideoSearchResponse { Provider = "internal", Page = request.Page, PerPage = request.PerPage, TotalResults = assets.Count };
        foreach (var asset in assets)
        {
            result.Items.Add(new StockVideoItemDto
            {
                Id = asset.Id, Provider = "internal", Title = asset.Title,
                DownloadUrl = asset.ObjectKey, PreviewUrl = await _storage.GeneratePresignedDownloadUrlAsync(asset.ObjectKey),
                ThumbnailUrl = string.IsNullOrWhiteSpace(asset.ThumbnailObjectKey) ? await _storage.GeneratePresignedDownloadUrlAsync(asset.ObjectKey) : await _storage.GeneratePresignedDownloadUrlAsync(asset.ThumbnailObjectKey),
                SourceUrl = asset.SourceUrl ?? string.Empty, AuthorName = asset.AuthorName, AuthorUrl = asset.AuthorUrl,
                DurationSeconds = asset.DurationSeconds ?? 0, Width = asset.Width ?? 0, Height = asset.Height ?? 0,
                Orientation = (asset.Height ?? 0) >= (asset.Width ?? 0) ? "portrait" : "landscape"
            });
        }
        return result;
    }

    private async Task<List<MediaLibraryAssetDto>> ToDtosAsync(IEnumerable<MediaLibraryAsset> assets)
    {
        var result = new List<MediaLibraryAssetDto>();
        foreach (var x in assets)
            result.Add(new MediaLibraryAssetDto
            {
                Id = x.Id, Type = x.Type, Title = x.Title, Description = x.Description, Tags = x.Tags,
                ObjectKey = x.ObjectKey, Url = await _storage.GeneratePresignedDownloadUrlAsync(x.ObjectKey),
                ThumbnailUrl = string.IsNullOrWhiteSpace(x.ThumbnailObjectKey) ? null : await _storage.GeneratePresignedDownloadUrlAsync(x.ThumbnailObjectKey),
                MimeType = x.MimeType, FileSizeBytes = x.FileSizeBytes, DurationSeconds = x.DurationSeconds,
                Width = x.Width, Height = x.Height, SourceUrl = x.SourceUrl, AuthorName = x.AuthorName,
                AuthorUrl = x.AuthorUrl, License = x.License, IsActive = x.IsActive, CreatedAt = x.CreatedAt
            });
        return result;
    }

    private static List<string> ParseTags(string? tags) => (tags ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    private static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    private static async Task ReadMediaInfoAsync(string path, MediaLibraryAsset asset)
    {
        var psi = new ProcessStartInfo { FileName = "ffprobe", Arguments = $"-v error -show_entries format=duration:stream=width,height -of json \"{path}\"", RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Không thể khởi động ffprobe.");
        var output = await process.StandardOutput.ReadToEndAsync(); await process.WaitForExitAsync();
        using var doc = JsonDocument.Parse(output);
        if (doc.RootElement.TryGetProperty("format", out var format) && format.TryGetProperty("duration", out var duration) && double.TryParse(duration.GetString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var seconds)) asset.DurationSeconds = (int)Math.Ceiling(seconds);
        if (doc.RootElement.TryGetProperty("streams", out var streams) && streams.GetArrayLength() > 0) { var stream = streams[0]; if (stream.TryGetProperty("width", out var width)) asset.Width = width.GetInt32(); if (stream.TryGetProperty("height", out var height)) asset.Height = height.GetInt32(); }
    }
}
