using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuotesApi.Data;
using QuotesApi.Models;

namespace Quotes.Tests.Integration;

public sealed class DatabaseTests
{
    [Fact]
    public async Task Startup_AppliesAllMigrations()
    {
        // Arrange
        using var factory = new QuotesApiFactory();
        _ = factory.CreateClient();

        // Act
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QuotesDbContext>();
        var applied = await db.Database.GetAppliedMigrationsAsync();
        var pending = await db.Database.GetPendingMigrationsAsync();

        // Assert
        applied.Should().BeEquivalentTo(db.Database.GetMigrations());
        pending.Should().BeEmpty();
    }

    [Fact]
    public async Task MigratedSchema_SupportsReadingAndWritingQuotes()
    {
        // Arrange
        using var factory = new QuotesApiFactory();
        var client = factory.CreateClient();

        // Act
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QuotesDbContext>();
        db.Quotes.Add(new Quote { Text = "Written straight through the migrated schema.", OwnerId = "1" });
        await db.SaveChangesAsync();

        // Assert
        var quotes = await client.GetFromJsonAsync<List<Quote>>("/api/quotes");
        quotes!.Should().Contain(quote => quote.Text == "Written straight through the migrated schema.");
    }

    [Fact]
    public async Task EachFactory_GetsItsOwnDatabase()
    {
        // Arrange
        using var first = new QuotesApiFactory();
        using var second = new QuotesApiFactory();
        var firstClient = first.CreateClient();
        var secondClient = second.CreateClient();

        var login = await firstClient.PostAsJsonAsync("/api/auth/login", new { email = "test@example.com", password = "password123" });
        var tokens = await login.Content.ReadFromJsonAsync<TokenResponse>();
        firstClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokens!.AccessToken);

        // Act
        await firstClient.PostAsJsonAsync("/api/quotes", new { text = "Only visible to the first factory." });

        // Assert
        var firstQuotes = await firstClient.GetFromJsonAsync<List<Quote>>("/api/quotes");
        var secondQuotes = await secondClient.GetFromJsonAsync<List<Quote>>("/api/quotes");
        firstQuotes!.Should().HaveCount(2);
        secondQuotes!.Should().HaveCount(1);
    }
}
