using System.Collections.Generic;
using System.Threading.Tasks;
using PostFlow.Models.DTOs.Requests;
using PostFlow.Models.DTOs.Responses;

namespace PostFlow.Services.Interfaces
{
    public interface ITrendService
    {
        /// <summary>Danh sách video xu hướng đọc từ dữ liệu đã thu thập (không gọi API nền tảng theo từng request).</summary>
        Task<TrendFeedResponse> GetTrendingTopicsAsync(TrendFeedQuery query);

        Task<TrendAnalysisResponse> AnalyzeTrendAsync(string userId, AnalyzeTrendRequest request);

        Task<TrendTopicDto> ImportManualAsync(string userId, ManualTrendImportRequest request);

        Task<IReadOnlyList<TrendSnapshotDto>> GetSnapshotsAsync(string trendVideoId);

        Task<TrendCollectionStatsDto> GetCollectionStatsAsync();

        TrendCollectionEnqueuedDto EnqueueDiscover(string? category);

        TrendCollectionEnqueuedDto EnqueueSnapshot();
    }
}
