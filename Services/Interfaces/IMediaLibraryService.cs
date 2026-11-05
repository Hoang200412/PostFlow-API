using PostFlow.Models.DTOs.Requests;
using PostFlow.Models.DTOs.Responses;

namespace PostFlow.Services.Interfaces;

public interface IMediaLibraryService
{
    Task<PagedResult<MediaLibraryAssetDto>> GetAsync(MediaLibraryQueryRequest request, CancellationToken cancellationToken = default);
    Task<MediaLibraryAssetDto> UploadAsync(string adminId, MediaLibraryUploadRequest request, CancellationToken cancellationToken = default);
    Task<MediaLibraryAssetDto> UpdateAsync(string id, MediaLibraryMetadataRequest request);
    Task<bool> DeleteAsync(string id);
    Task<StockVideoSearchResponse> SearchVideosAsync(SearchStockVideoRequest request, CancellationToken cancellationToken = default);
}
