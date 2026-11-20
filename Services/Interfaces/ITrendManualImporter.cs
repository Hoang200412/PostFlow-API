using System.Threading;
using System.Threading.Tasks;
using PostFlow.Models;
using PostFlow.Models.DTOs.Requests;

namespace PostFlow.Services.Interfaces
{
    public interface ITrendManualImporter
    {
        /// <summary>
        /// Thêm video từ link. YouTube: số liệu lấy từ API. TikTok/Reels: metadata từ oEmbed công khai,
        /// số liệu do người dùng nhập (đánh dấu manual), vì các nền tảng này không có API công khai cho video của người khác.
        /// </summary>
        Task<TrendVideo> ImportAsync(string userId, ManualTrendImportRequest request, CancellationToken cancellationToken = default);
    }
}
