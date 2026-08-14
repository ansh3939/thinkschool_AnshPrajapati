using Microsoft.IdentityModel.Tokens;

namespace QuotesApi.Authentication;

public sealed record JwtOptions
{
    public string? SigningKey { get; init; }
    public string? Issuer { get; init; }
    public string? Audience { get; init; }
    public TimeSpan AccessTokenLifetime { get; init; } = TimeSpan.FromMinutes(15);
}

public static class JwtOptionsValidator
{
    public static SymmetricSecurityKey GetSigningKey(JwtOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Issuer) || string.IsNullOrWhiteSpace(options.Audience))
            throw new InvalidOperationException("JWT issuer and audience must be configured.");

        try
        {
            var keyBytes = Convert.FromBase64String(options.SigningKey ?? string.Empty);
            if (keyBytes.Length != 32)
                throw new InvalidOperationException("JWT signing key must be a Base64-encoded 32-byte value.");

            return new SymmetricSecurityKey(keyBytes);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException("JWT signing key must be a valid Base64 value.", exception);
        }
    }
}
