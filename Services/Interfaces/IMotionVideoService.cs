using PostFlow.Models.DTOs.Requests;
using PostFlow.Models.DTOs.Responses;

namespace PostFlow.Services.Interfaces;

public interface IMotionVideoService
{
    Task<StockVideoSearchResponse> SearchStockVideosAsync(
        SearchStockVideoRequest request,
        CancellationToken cancellationToken = default);

    string CreateRenderJob(string userId, RenderMotionVideoRequest request);
    MotionVideoJobStatusDto? GetJob(string jobId, string userId);
    Task<string> UploadMusicAsync(string userId, IFormFile file, CancellationToken cancellationToken = default);
}
