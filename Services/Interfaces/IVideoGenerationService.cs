using System.Threading.Tasks;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Models.DTOs.Responses;

namespace PostFlow.Services.Interfaces;

public interface IVideoGenerationService
{
    Task<string> GenerateVideoAsync(
        string userId,
        string topic,
        string? contentDnaId,
        string scriptJson,
        string? voice);

    Task<VoiceoverPreviewResponse> GenerateVoiceoverAudioAsync(
        string userId,
        GenerateVoiceoverRequest request);

    Task<VideoAssetDto> DubVideoAsync(
        string userId,
        DubVideoRequest request);
}
