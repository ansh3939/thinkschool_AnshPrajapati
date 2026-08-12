using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using QuotesApi.Data;
using QuotesApi.Models;

namespace Quotes.Tests.Integration;

public sealed class QuoteEndpointTests
{
    [Fact]
    public async Task GetQuotes_ReturnsSeededQuote()
    {
        // Arrange
        using var factory = new QuotesApiFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/quotes");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var quotes = await response.Content.ReadFromJsonAsync<List<Quote>>();
        quotes.Should().ContainSingle()
            .Which.Text.Should().Be("JWT authentication protects quote changes.");
    }

    [Fact]
    public async Task CreateQuote_WithValidToken_ReturnsCreated()
    {
        // Arrange
        using var factory = new QuotesApiFactory();
        var client = factory.CreateClient();
        await AuthenticateAsync(client);

        // Act
        var response = await client.PostAsJsonAsync("/api/quotes", new { text = "  Tests are cheaper than incidents.  " });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<Quote>();
        created!.Text.Should().Be("Tests are cheaper than incidents.");
        created.OwnerId.Should().Be("1");
        response.Headers.Location!.ToString().Should().Be($"/api/quotes/{created.Id}");

        var quotes = await client.GetFromJsonAsync<List<Quote>>("/api/quotes");
        quotes!.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateQuote_WithoutToken_ReturnsUnauthorized()
    {
        // Arrange
        using var factory = new QuotesApiFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/quotes", new { text = "Anonymous writers are not welcome." });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateQuote_WithInvalidToken_ReturnsUnauthorized()
    {
        // Arrange
        using var factory = new QuotesApiFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-real-token");

        // Act
        var response = await client.PostAsJsonAsync("/api/quotes", new { text = "Forged tokens do not work." });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateQuote_EmptyText_ReturnsValidationProblemDetails(string? text)
    {
        // Arrange
        using var factory = new QuotesApiFactory();
        var client = factory.CreateClient();
        await AuthenticateAsync(client);

        // Act
        var response = await client.PostAsJsonAsync("/api/quotes", new { text });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem!.Status.Should().Be(StatusCodes.Status400BadRequest);
        problem.Errors.Should().ContainKey("text");
        problem.Errors["text"].Should().Contain("Text is required.");
    }

    [Fact]
    public async Task DeleteQuote_OwnQuote_ReturnsNoContent()
    {
        // Arrange
        using var factory = new QuotesApiFactory();
        var client = factory.CreateClient();
        await AuthenticateAsync(client);
        var created = await client.PostAsJsonAsync("/api/quotes", new { text = "This one gets deleted." });
        var quote = await created.Content.ReadFromJsonAsync<Quote>();

        // Act
        var response = await client.DeleteAsync($"/api/quotes/{quote!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var quotes = await client.GetFromJsonAsync<List<Quote>>("/api/quotes");
        quotes!.Should().NotContain(remaining => remaining.Id == quote.Id);
    }

    [Fact]
    public async Task DeleteQuote_NonexistentId_ReturnsNotFound()
    {
        // Arrange
        using var factory = new QuotesApiFactory();
        var client = factory.CreateClient();
        await AuthenticateAsync(client);

        // Act
        var response = await client.DeleteAsync("/api/quotes/9999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteQuote_WithoutToken_ReturnsUnauthorized()
    {
        // Arrange
        using var factory = new QuotesApiFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.DeleteAsync("/api/quotes/1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteQuote_OwnedByAnotherUser_ReturnsForbidden()
    {
        // Arrange - a second user who does not own the seeded quote
        using var factory = new QuotesApiFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QuotesDbContext>();
            db.Users.Add(new User
            {
                Email = "other@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123")
            });
            await db.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        await AuthenticateAsync(client, "other@example.com");
        var quotes = await client.GetFromJsonAsync<List<Quote>>("/api/quotes");

        // Act
        var response = await client.DeleteAsync($"/api/quotes/{quotes![0].Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task AuthenticateAsync(HttpClient client, string email = "test@example.com")
    {
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "password123" });
        login.EnsureSuccessStatusCode();
        var tokens = await login.Content.ReadFromJsonAsync<TokenResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
    }
}
