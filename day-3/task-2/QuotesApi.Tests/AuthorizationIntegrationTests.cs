using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using QuotesApi.Data;
using QuotesApi.Models;

namespace QuotesApi.Tests;

public class AuthorizationIntegrationTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task Create_quote_with_quotes_write_scope_succeeds()
    {
        var client = CreateAuthenticatedClient("writer", new Claim("scope", "quotes.write"));

        var response = await client.PostAsJsonAsync("/api/quotes", new CreateQuoteRequest("Scoped quote"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Create_quote_without_quotes_write_scope_returns_forbidden()
    {
        var client = CreateAuthenticatedClient("reader");

        var response = await client.PostAsJsonAsync("/api/quotes", new CreateQuoteRequest("Unscoped quote"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_own_quote_succeeds()
    {
        var quoteId = await AddQuote("owner-1");
        var client = CreateAuthenticatedClient("owner-1");

        var response = await client.DeleteAsync($"/api/quotes/{quoteId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Delete_another_users_quote_returns_forbidden()
    {
        var quoteId = await AddQuote("owner-2");
        var client = CreateAuthenticatedClient("owner-1");

        var response = await client.DeleteAsync($"/api/quotes/{quoteId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private HttpClient CreateAuthenticatedClient(string userId, params Claim[] claims)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateAccessToken(
            [new Claim(JwtRegisteredClaimNames.Sub, userId), .. claims]));
        return client;
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

    private static string CreateAccessToken(Claim[] claims)
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
}
