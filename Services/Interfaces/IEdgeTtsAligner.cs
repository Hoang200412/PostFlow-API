using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PostFlow.Services.Interfaces
{
    public sealed record SpokenWord(string Text, double StartMs, double EndMs);

    public sealed record NarrationAudio(string AudioPath, double TotalDurationSeconds, IReadOnlyList<SpokenWord> Words);

    public sealed record VoiceOptions(string? Voice, string? Rate, string? Pitch);

    public interface IEdgeTtsAligner
    {
        /// <summary>Sinh audio giọng đọc kèm mốc thời gian từng từ.</summary>
        Task<NarrationAudio> SynthesizeAsync(string narrationText, VoiceOptions voice, string workDirectory, CancellationToken cancellationToken = default);
    }
}
