using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PostFlow.Services.Interfaces;

public record TtsVoiceInfo(
    string Id,
    string VoiceId,
    string Name,
    string Gender,
    string Language,
    string Accent,
    string Provider,
    string SampleText,
    string? SampleAudioObjectKey,
    string? SampleAudioUrl,
    bool IsActive,
    bool IsDefault,
    int SortOrder);

public interface ITtsService
{
    /// <summary>
    /// Gọi PostFlow-TTS sinh audio và ghi ra file. Không tự validate text ở BE;
    /// lỗi 400 từ tầng TTS được chuyển thành BadRequestException.
    /// </summary>
    Task SynthesizeToFileAsync(
        string text,
        string? voice,
        string? rate,
        string? pitch,
        string outputPath,
        CancellationToken cancellationToken = default);

    /// <summary>Lấy danh sách giọng đọc từ DB (chỉ lấy giọng đang bật nếu activeOnly = true).</summary>
    Task<IReadOnlyList<TtsVoiceInfo>> GetVoicesAsync(bool activeOnly = true, CancellationToken cancellationToken = default);

    /// <summary>Kiểm tra mã giọng có hợp lệ trong DB (hoặc nằm trong danh sách map tương thích cũ).</summary>
    Task<bool> IsSupportedVoiceAsync(string? voice, CancellationToken cancellationToken = default);
}
