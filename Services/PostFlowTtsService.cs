using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PostFlow.Configuration;
using PostFlow.Data;
using PostFlow.Exceptions;
using PostFlow.Services.Interfaces;

namespace PostFlow.Services;

public class PostFlowTtsService : ITtsService
{
    private static readonly IReadOnlyDictionary<string, string> LegacyVoiceMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["vi-VN-HoaiMyNeural"] = "diem_trinh",
            ["vi-VN-NamMinhNeural"] = "hung_thinh",
            ["en-US-JennyNeural"] = "mai_linh",
            ["en-US-AriaNeural"] = "ngoc_huyen",
            ["en-US-GuyNeural"] = "manh_dung",
            ["en-US-ChristopherNeural"] = "tuan_ngoc",
        };

    private static readonly Regex RateRegex = new(@"^([+-]?\d+(?:\.\d+)?)\s*%?$", RegexOptions.Compiled);

    private readonly HttpClient _httpClient;
    private readonly AppDbContext _dbContext;
    private readonly PostFlowTtsSettings _settings;
    private readonly ILogger<PostFlowTtsService> _logger;

    public PostFlowTtsService(
        HttpClient httpClient,
        AppDbContext dbContext,
        IOptions<PostFlowTtsSettings> settings,
        ILogger<PostFlowTtsService> logger)
    {
        _httpClient = httpClient;
        _dbContext = dbContext;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SynthesizeToFileAsync(
        string text,
        string? voice,
        string? rate,
        string? pitch,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
        {
            throw new InvalidOperationException("Dịch vụ PostFlow-TTS hiện đang tắt trong cấu hình.");
        }

        if (!string.IsNullOrWhiteSpace(pitch) && pitch != "+0Hz" && pitch != "0Hz")
        {
            _logger.LogDebug("PostFlow-TTS không hỗ trợ tham số pitch ({Pitch}); tham số này được bỏ qua.", pitch);
        }

        var resolvedVoice = await ResolveVoiceAsync(voice, cancellationToken);
        var speed = ParseRateToSpeed(rate);

        // Truyền nguyên text (không tự validate ở BE) để tầng PostFlow-TTS kiểm tra và trả lỗi chuẩn.
        var payload = new
        {
            text = text ?? string.Empty,
            voice = resolvedVoice,
            format = "mp3",
            speed
        };

        using var response = await _httpClient.PostAsJsonAsync("/tts", payload, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var errorMessage = ExtractErrorMessage(body) ?? $"PostFlow-TTS trả về lỗi HTTP {(int)response.StatusCode}.";

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                throw new BadRequestException(errorMessage);
            }

            _logger.LogError("PostFlow-TTS lỗi ({StatusCode}): {Body}", (int)response.StatusCode, body);
            throw new InvalidOperationException(errorMessage);
        }

        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = File.Create(outputPath);
        await responseStream.CopyToAsync(fileStream, cancellationToken);
    }

    public async Task<IReadOnlyList<TtsVoiceInfo>> GetVoicesAsync(
        bool activeOnly = true,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.TtsVoices.AsNoTracking();
        if (activeOnly)
        {
            query = query.Where(v => v.IsActive);
        }

        return await query
            .OrderBy(v => v.SortOrder)
            .ThenBy(v => v.Name)
            .Select(v => new TtsVoiceInfo(
                v.Id,
                v.VoiceId,
                v.Name,
                v.Gender,
                v.Language,
                v.Accent,
                v.Provider,
                v.SampleText,
                v.SampleAudioObjectKey,
                v.SampleAudioUrl,
                v.IsActive,
                v.IsDefault,
                v.SortOrder))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> IsSupportedVoiceAsync(string? voice, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(voice))
        {
            return true;
        }

        var trimmed = voice.Trim();
        if (LegacyVoiceMap.ContainsKey(trimmed))
        {
            return true;
        }

        return await _dbContext.TtsVoices
            .AsNoTracking()
            .AnyAsync(v => v.IsActive && v.VoiceId == trimmed, cancellationToken);
    }

    internal static string MapLegacyVoice(string? voice, string fallbackVoice)
    {
        if (string.IsNullOrWhiteSpace(voice))
        {
            return fallbackVoice;
        }

        var trimmed = voice.Trim();
        return LegacyVoiceMap.TryGetValue(trimmed, out var mapped) ? mapped : trimmed;
    }

    internal static double ParseRateToSpeed(string? rate)
    {
        if (string.IsNullOrWhiteSpace(rate))
        {
            return 1.0;
        }

        var match = RateRegex.Match(rate.Trim());
        if (!match.Success || !double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var percent))
        {
            return 1.0;
        }

        var speed = 1.0 + (percent / 100.0);
        return Math.Clamp(Math.Round(speed, 2), 0.5, 2.0);
    }

    private async Task<string> ResolveVoiceAsync(string? requestedVoice, CancellationToken cancellationToken)
    {
        var fallback = _settings.DefaultVoice;
        if (string.IsNullOrWhiteSpace(requestedVoice))
        {
            var dbDefault = await _dbContext.TtsVoices
                .AsNoTracking()
                .Where(v => v.IsActive && v.IsDefault)
                .Select(v => v.VoiceId)
                .FirstOrDefaultAsync(cancellationToken);

            return string.IsNullOrWhiteSpace(dbDefault) ? fallback : dbDefault;
        }

        return MapLegacyVoice(requestedVoice, fallback);
    }

    private static string? ExtractErrorMessage(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("message", out var msgProp) && msgProp.ValueKind == JsonValueKind.String)
            {
                return msgProp.GetString();
            }
        }
        catch (JsonException)
        {
            // Bỏ qua nếu response không phải JSON
        }

        return null;
    }
}
