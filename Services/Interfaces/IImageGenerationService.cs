using System.Threading.Tasks;

namespace PostFlow.Services.Interfaces;

public interface IImageGenerationService
{
    Task<string> GenerateImageUrlAsync(
        string prompt,
        string style,
        string aspectRatio,
        string colorTone,
        string mood,
        bool includeText,
        string? referenceImageUrl = null);
}
