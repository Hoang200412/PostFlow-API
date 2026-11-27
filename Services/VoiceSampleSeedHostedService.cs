using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PostFlow.Configuration;

namespace PostFlow.Services;

/// <summary>
/// Tự động sinh 6 file audio mẫu ("Xin chào, tôi là ...") qua PostFlow-TTS, upload lên R2 và lưu URL vào bảng TtsVoices.
/// </summary>
public class VoiceSampleSeedHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly PostFlowTtsSettings _ttsSettings;
    private readonly ILogger<VoiceSampleSeedHostedService> _logger;

    public VoiceSampleSeedHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<PostFlowTtsSettings> ttsSettings,
        ILogger<VoiceSampleSeedHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _ttsSettings = ttsSettings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_ttsSettings.Enabled)
        {
            return;
        }

        // Chờ app khởi động xong và PostFlow-TTS sẵn sàng
        int[] retryDelaysSeconds = [3, 10, 20, 40];
        foreach (var delay in retryDelaysSeconds)
        {
            await Task.Delay(TimeSpan.FromSeconds(delay), stoppingToken);

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var sampleService = scope.ServiceProvider.GetRequiredService<IVoiceSampleService>();
                var count = await sampleService.SeedMissingSamplesAsync(stoppingToken);
                if (count > 0)
                {
                    _logger.LogInformation("Đã seed xong {Count} file audio mẫu giọng đọc lên R2.", count);
                }
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Chưa thể seed audio mẫu giọng đọc từ PostFlow-TTS (sẽ thử lại nếu còn lượt).");
            }
        }
    }
}
