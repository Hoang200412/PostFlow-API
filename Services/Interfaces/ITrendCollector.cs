using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using PostFlow.Models;
using PostFlow.Models.DTOs.Responses;

namespace PostFlow.Services.Interfaces
{
    public interface ITrendCollector
    {
        /// <summary>Tìm video mới cho một danh mục (hoặc tất cả khi null): chart phổ biến, mẫu viral và mẫu đối chứng.</summary>
        [AutomaticRetry(Attempts = 0)]
        [DisableConcurrentExecution(timeoutInSeconds: 1800)]
        Task<TrendCollectionSummary> DiscoverAsync(string? categorySlug);

        /// <summary>Chụp snapshot số liệu cho các video đang tới mốc tuổi 24h/72h/7 ngày.</summary>
        [AutomaticRetry(Attempts = 0)]
        [DisableConcurrentExecution(timeoutInSeconds: 1800)]
        Task<TrendCollectionSummary> SnapshotDueAsync();

        /// <summary>Thêm một video YouTube cụ thể (người dùng dán link) với số liệu lấy từ API.</summary>
        Task<TrendVideo> ImportYouTubeVideoAsync(string videoId, string categorySlug, string? userId, CancellationToken cancellationToken = default);

        Task<TrendQuotaStatus> GetQuotaStatusAsync();
    }
}
