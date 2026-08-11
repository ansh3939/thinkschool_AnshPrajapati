using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace QuotesApi.Tests;

public class AuthenticationIntegrationTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task Login_with_valid_credentials_succeeds()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest("test@example.com", "password123"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tokens = await response.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.False(string.IsNullOrWhiteSpace(tokens!.access_token));
        Assert.False(string.IsNullOrWhiteSpace(tokens.refresh_token));
        Assert.Equal(3600, tokens.expires_in);
    }

    [Fact]
    public async Task Login_with_invalid_credentials_returns_unauthorized()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest("test@example.com", "wrong-password"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_quotes_is_public()
    {
        var response = await factory.CreateClient().GetAsync("/api/quotes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Post_quotes_without_a_token_returns_unauthorized()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/quotes", new CreateQuoteRequest("Unauthenticated quote"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_quotes_with_a_valid_token_succeeds()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await GetAccessToken(client));

        var response = await client.PostAsJsonAsync("/api/quotes", new CreateQuoteRequest("Authenticated quote"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Expired_jwt_returns_unauthorized_with_a_bearer_challenge()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateExpiredToken());

        var response = await client.PostAsJsonAsync("/api/quotes", new CreateQuoteRequest("Expired token quote"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("Bearer", response.Headers.WwwAuthenticate.ToString());
        Assert.Contains("invalid_token", response.Headers.WwwAuthenticate.ToString());
    }

    [Fact]
    public async Task Delete_without_a_token_returns_unauthorized()
    {
        var response = await factory.CreateClient().DeleteAsync("/api/quotes/1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Delete_with_a_valid_token_succeeds()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await GetAccessToken(client));

        var response = await client.DeleteAsync("/api/quotes/1");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static async Task<string> GetAccessToken(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("test@example.com", "password123"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TokenResponse>())!.access_token;
    }

    private static string CreateExpiredToken()
    {
        using var configuration = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "appsettings.json")));
        var jwt = configuration.RootElement.GetProperty("Jwt");
        var key = new SymmetricSecurityKey(Convert.FromBase64String(jwt.GetProperty("Key").GetString()!));
        var token = new JwtSecurityToken(
            issuer: jwt.GetProperty("Issuer").GetString(),
            audience: jwt.GetProperty("Audience").GetString(),
            claims: [new Claim(JwtRegisteredClaimNames.Sub, "1")],
            notBefore: DateTime.UtcNow.AddHours(-2),
            expires: DateTime.UtcNow.AddHours(-1),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed record TokenResponse(string access_token, string refresh_token, int expires_in);
}
