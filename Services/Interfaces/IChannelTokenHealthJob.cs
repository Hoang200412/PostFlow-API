using System.Threading.Tasks;

namespace PostFlow.Services.Interfaces
{
    public interface IChannelTokenHealthJob
    {
        /// <summary>Quét các kênh có token sắp hết hạn hoặc cần kết nối lại và gửi thông báo cho chủ kênh.</summary>
        Task NotifyUnhealthyChannelsAsync();
    }
}
