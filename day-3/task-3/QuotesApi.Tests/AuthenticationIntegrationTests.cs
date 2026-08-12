using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.DependencyInjection;
using QuotesApi.Data;
using QuotesApi.Models;

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
        Assert.Equal(900, tokens.expires_in);
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
    public async Task Authenticated_user_without_quotes_write_scope_returns_forbidden()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateAccessToken(
            new Claim(JwtRegisteredClaimNames.Sub, "reader")));

        var response = await client.PostAsJsonAsync("/api/quotes", new CreateQuoteRequest("Forbidden quote"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Authenticated_user_with_quotes_write_scope_can_create_a_quote()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateAccessToken(
            new Claim(JwtRegisteredClaimNames.Sub, "writer"),
            new Claim("scope", "quotes.write")));

        var response = await client.PostAsJsonAsync("/api/quotes", new CreateQuoteRequest("Allowed quote"));

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

    [Fact]
    public async Task User_can_delete_their_own_quote()
    {
        var quoteId = await AddQuote("owner-1");
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateAccessToken(
            new Claim(JwtRegisteredClaimNames.Sub, "owner-1")));

        var response = await client.DeleteAsync($"/api/quotes/{quoteId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task User_cannot_delete_another_users_quote_returns_forbidden()
    {
        var quoteId = await AddQuote("owner-2");
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateAccessToken(
            new Claim(JwtRegisteredClaimNames.Sub, "owner-1")));

        var response = await client.DeleteAsync($"/api/quotes/{quoteId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_with_a_valid_refresh_token_returns_a_new_token_pair()
    {
        var client = factory.CreateClient();
        var loginTokens = await Login(client);

        var response = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest(loginTokens.refresh_token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var refreshedTokens = await response.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.False(string.IsNullOrWhiteSpace(refreshedTokens!.access_token));
        Assert.NotEqual(loginTokens.refresh_token, refreshedTokens.refresh_token);
        Assert.Equal(900, refreshedTokens.expires_in);
    }

    [Fact]
    public async Task Old_refresh_token_is_rejected_after_rotation()
    {
        var client = factory.CreateClient();
        var loginTokens = await Login(client);
        await client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest(loginTokens.refresh_token));

        var response = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest(loginTokens.refresh_token));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Expired_refresh_token_is_rejected()
    {
        var client = factory.CreateClient();
        var loginTokens = await Login(client);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QuotesDbContext>();
            var token = await db.RefreshTokens.SingleAsync(token => token.Token == HashRefreshToken(loginTokens.refresh_token));
            token.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }

        var response = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest(loginTokens.refresh_token));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_refresh_token_is_rejected()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest("unknown-refresh-token"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_the_refresh_token()
    {
        var client = factory.CreateClient();
        var loginTokens = await Login(client);

        var logoutResponse = await client.PostAsJsonAsync("/api/auth/logout", new RefreshTokenRequest(loginTokens.refresh_token));
        var refreshResponse = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest(loginTokens.refresh_token));

        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }

    [Fact]
    public async Task Reusing_rotated_refresh_token_revokes_the_entire_token_family()
    {
        var client = factory.CreateClient();
        var tokenA = await Login(client);
        var refreshResponse = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest(tokenA.refresh_token));
        var tokenB = (await refreshResponse.Content.ReadFromJsonAsync<TokenResponse>())!;

        // Token A -> Token B -> Token A reused -> entire family revoked -> Token B rejected.
        var reuseResponse = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest(tokenA.refresh_token));
        var tokenBResponse = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest(tokenB.refresh_token));

        Assert.Equal(HttpStatusCode.Unauthorized, reuseResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, tokenBResponse.StatusCode);
    }

    private static async Task<string> GetAccessToken(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("test@example.com", "password123"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TokenResponse>())!.access_token;
    }

    private static async Task<TokenResponse> Login(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("test@example.com", "password123"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TokenResponse>())!;
    }

    private static string HashRefreshToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

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

    private async Task<int> AddQuote(string ownerId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<QuotesDbContext>();
        var quote = new Quote { Text = "Test quote", OwnerId = ownerId };
        db.Quotes.Add(quote);
        await db.SaveChangesAsync();
        return quote.Id;
    }

    private static string CreateAccessToken(params Claim[] claims)
    {
        using var configuration = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "appsettings.json")));
        var jwt = configuration.RootElement.GetProperty("Jwt");
        var key = new SymmetricSecurityKey(Convert.FromBase64String(jwt.GetProperty("Key").GetString()!));
        var token = new JwtSecurityToken(
            issuer: jwt.GetProperty("Issuer").GetString(),
            audience: jwt.GetProperty("Audience").GetString(),
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed record TokenResponse(string access_token, string refresh_token, int expires_in);
    private sealed record RefreshTokenRequest(string refreshToken);
}
