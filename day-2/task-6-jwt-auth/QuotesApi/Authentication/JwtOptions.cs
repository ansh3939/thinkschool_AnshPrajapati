using Microsoft.IdentityModel.Tokens;

namespace QuotesApi.Authentication;

public sealed class JwtOptions
{
    public string? Key { get; init; }
    public string? Issuer { get; init; }
    public string? Audience { get; init; }
    public int AccessTokenLifetimeMinutes { get; init; } = 60;
}

public static class JwtOptionsValidator
{
    public static SymmetricSecurityKey GetSigningKey(JwtOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Issuer) || string.IsNullOrWhiteSpace(options.Audience))
            throw new InvalidOperationException("JWT issuer and audience must be configured.");

        try
        {
            var keyBytes = Convert.FromBase64String(options.Key ?? string.Empty);
            if (keyBytes.Length != 32)
                throw new InvalidOperationException("JWT key must be a Base64-encoded 32-byte value.");

            return new SymmetricSecurityKey(keyBytes);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException("JWT key must be a valid Base64 value.", exception);
        }
    }
}
