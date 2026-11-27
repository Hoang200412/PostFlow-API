using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PostFlow.Data;
using PostFlow.Models;
using PostFlow.Services.Interfaces;

namespace PostFlow.Services;

public interface IVoiceSampleService
{
    Task<TtsVoice> GenerateAndSaveSampleAsync(TtsVoice voice, CancellationToken cancellationToken = default);
    Task<int> SeedMissingSamplesAsync(CancellationToken cancellationToken = default);
}

public class VoiceSampleService : IVoiceSampleService
{
    private readonly AppDbContext _dbContext;
    private readonly ITtsService _ttsService;
    private readonly IStorageService _storageService;
    private readonly ILogger<VoiceSampleService> _logger;

    public VoiceSampleService(
        AppDbContext dbContext,
        ITtsService ttsService,
        IStorageService storageService,
        ILogger<VoiceSampleService> logger)
    {
        _dbContext = dbContext;
        _ttsService = ttsService;
        _storageService = storageService;
        _logger = logger;
    }

    public async Task<TtsVoice> GenerateAndSaveSampleAsync(TtsVoice voice, CancellationToken cancellationToken = default)
    {
        var sampleText = string.IsNullOrWhiteSpace(voice.SampleText)
            ? $"Xin chào, tôi là {voice.Name}."
            : voice.SampleText.Trim();

        var tempFile = Path.Combine(Path.GetTempPath(), $"voice_sample_{voice.VoiceId}_{Guid.NewGuid():N}.mp3");
        try
        {
            await _ttsService.SynthesizeToFileAsync(
                sampleText,
                voice.VoiceId,
                null,
                null,
                tempFile,
                cancellationToken);

            await using var stream = File.OpenRead(tempFile);
            var objectKey = await _storageService.SaveFileAsync(
                stream,
                "audio/voices",
                $"{voice.VoiceId}.mp3",
                "audio/mpeg");

            var url = await _storageService.GeneratePresignedDownloadUrlAsync(objectKey, 24 * 365);

            voice.SampleText = sampleText;
            voice.SampleAudioObjectKey = objectKey;
            voice.SampleAudioUrl = url;
            voice.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Đã tạo audio mẫu R2 cho giọng {VoiceId}: {Url}", voice.VoiceId, url);
            return voice;
        }
        finally
        {
            try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
        }
    }

    public async Task<int> SeedMissingSamplesAsync(CancellationToken cancellationToken = default)
    {
        var missingVoices = await _dbContext.TtsVoices
            .Where(v => string.IsNullOrEmpty(v.SampleAudioUrl))
            .OrderBy(v => v.SortOrder)
            .ToListAsync(cancellationToken);

        if (missingVoices.Count == 0)
        {
            return 0;
        }

        var generated = 0;
        foreach (var voice in missingVoices)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await GenerateAndSaveSampleAsync(voice, cancellationToken);
            generated++;
        }

        return generated;
    }
}
