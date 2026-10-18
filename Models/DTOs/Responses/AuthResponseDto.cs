using System.Text.Json.Serialization;

namespace PostFlow.Models.DTOs.Responses
{
    public class AuthResponseDto
    {
        public string Token { get; set; } = string.Empty;

        [JsonIgnore]
        public string RefreshToken { get; set; } = string.Empty;

        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
    }
}
