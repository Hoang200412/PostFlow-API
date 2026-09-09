using PostFlow.Models;

namespace PostFlow.Models.DTOs.Responses
{
    public class OAuthLoginUrlResponse
    {
        public PlatformType Platform { get; set; }
        public string AuthUrl { get; set; } = string.Empty;
    }
}
