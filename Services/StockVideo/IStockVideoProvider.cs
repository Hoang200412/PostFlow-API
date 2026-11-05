using PostFlow.Models.DTOs.Requests;
using PostFlow.Models.DTOs.Responses;

namespace PostFlow.Services.StockVideo;

public interface IStockVideoProvider
{
    string Name { get; }
    Task<StockVideoSearchResponse> SearchAsync(
        SearchStockVideoRequest request,
        CancellationToken cancellationToken = default);
}
