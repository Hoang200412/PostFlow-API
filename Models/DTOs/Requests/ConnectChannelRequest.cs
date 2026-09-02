using PostFlow.Models;

namespace PostFlow.Models.DTOs.Requests
{
    public class ConnectChannelRequest
    {
        public PlatformType Platform { get; set; }
        public string AccountName { get; set; } = string.Empty;
        public string ChannelIdOrPageId { get; set; } = string.Empty;
    }
}
