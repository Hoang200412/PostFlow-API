using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PostFlow.Services.Trends;

namespace PostFlow.Services.Interfaces
{
    public interface IYouTubeDataClient
    {
        bool IsConfigured { get; }

        /// <summary>search.list với videoDuration=short. Tốn 100 đơn vị quota mỗi trang.</summary>
        Task<YouTubeSearchPage> SearchShortVideoIdsAsync(YouTubeSearchQuery query, CancellationToken cancellationToken = default);

        /// <summary>videos.list theo id (tối đa 50). Tốn 1 đơn vị quota.</summary>
        Task<IReadOnlyList<YouTubeVideoItem>> GetVideosAsync(IReadOnlyCollection<string> videoIds, CancellationToken cancellationToken = default);

        /// <summary>videos.list chart=mostPopular. Tốn 1 đơn vị quota. Danh mục không có chart → danh sách rỗng.</summary>
        Task<IReadOnlyList<YouTubeVideoItem>> GetMostPopularAsync(string regionCode, string? categoryId, int maxResults, CancellationToken cancellationToken = default);

        /// <summary>channels.list theo id (tối đa 50). Tốn 1 đơn vị quota.</summary>
        Task<IReadOnlyList<YouTubeChannelItem>> GetChannelsAsync(IReadOnlyCollection<string> channelIds, CancellationToken cancellationToken = default);
    }
}
