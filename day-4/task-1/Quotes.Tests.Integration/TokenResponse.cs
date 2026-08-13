using System.Text.Json.Serialization;

namespace Quotes.Tests.Integration;

/// <summary>Shape of the token payload returned by /api/auth/login and /api/auth/refresh.</summary>
public sealed record TokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; init; } = string.Empty;

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; init; } = string.Empty;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; init; }
}
