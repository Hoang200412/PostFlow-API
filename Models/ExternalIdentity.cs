using System;

namespace PostFlow.Models;

public sealed class ExternalIdentity
{
    public string Provider { get; init; } = string.Empty;
    public PlatformType Platform { get; init; }
    public string ProviderUserId { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string AccessToken { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
    public DateTime? TokenExpiresAt { get; init; }
}
