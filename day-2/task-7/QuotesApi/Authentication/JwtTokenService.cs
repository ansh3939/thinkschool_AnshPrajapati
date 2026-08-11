using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using QuotesApi.Models;

namespace QuotesApi.Authentication;

public sealed class JwtTokenService(JwtOptions options)
{
    private readonly SigningCredentials _credentials = new(
        JwtOptionsValidator.GetSigningKey(options), SecurityAlgorithms.HmacSha256);

    public string CreateAccessToken(User user)
    {
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email)
            ],
            notBefore: now,
            expires: now.AddMinutes(options.AccessTokenLifetimeMinutes),
            signingCredentials: _credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
