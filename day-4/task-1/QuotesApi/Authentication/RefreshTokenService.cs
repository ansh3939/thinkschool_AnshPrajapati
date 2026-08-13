using System.Security.Cryptography;

namespace QuotesApi.Authentication;

public sealed class RefreshTokenService
{
    public const int LifetimeDays = 7;

    public string CreateToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    public string HashToken(string token) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
}
