using System.Threading;
using System.Threading.Tasks;
using PostFlow.Services.Trends;

namespace PostFlow.Services.Interfaces
{
    public interface ITrendOEmbedClient
    {
        /// <summary>TikTok oEmbed công khai, không cần key.</summary>
        Task<OEmbedInfo?> GetTikTokAsync(string url, CancellationToken cancellationToken = default);

        /// <summary>Facebook oEmbed (oembed_video) cần app access token; trả null nếu chưa cấu hình app.</summary>
        Task<OEmbedInfo?> GetFacebookVideoAsync(string url, CancellationToken cancellationToken = default);
    }
}
