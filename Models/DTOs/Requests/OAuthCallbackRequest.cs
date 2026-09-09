namespace PostFlow.Models.DTOs.Requests
{
    public class OAuthCallbackRequest
    {
        public string Code { get; set; } = string.Empty;
        public string RedirectUri { get; set; } = string.Empty;
    }
}
