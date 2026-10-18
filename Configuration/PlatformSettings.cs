namespace PostFlow.Configuration
{
    public class PlatformSettings
    {
        public YouTubeSettings YouTube { get; set; } = new();
        public TikTokSettings TikTok { get; set; } = new();
        public FacebookReelsSettings FacebookReels { get; set; } = new();
    }

    public class YouTubeSettings
    {
        public string ApplicationName { get; set; } = "PostFlow";
        public string ApiKey { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class TikTokSettings
    {
        public string ClientKey { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string AccessToken { get; set; } = string.Empty;
        public string OpenId { get; set; } = string.Empty;
    }

    public class FacebookReelsSettings
    {
        public string AppId { get; set; } = string.Empty;
        public string AppSecret { get; set; } = string.Empty;
        public string PageId { get; set; } = string.Empty;
        public string PageAccessToken { get; set; } = string.Empty;
        public string LoginAppId { get; set; } = string.Empty;
        public string LoginAppSecret { get; set; } = string.Empty;
    }
}
