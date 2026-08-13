using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuotesApi.Authentication;
using QuotesApi.Data;

namespace Quotes.Tests.Integration;

[Collection(SqlServerCollection.Name)]
public sealed class AuthEndpointTests(SqlServerContainerFixture sqlServer)
{
    private const string SeededEmail = "test@example.com";
    private const string SeededPassword = "password123";

    [Fact]
    public async Task Login_ValidCredentials_ReturnsTokens()
    {
        // Arrange
        using var factory = new QuotesApiFactory(sqlServer);
        var client = factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = SeededEmail, password = SeededPassword });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokens = await response.Content.ReadFromJsonAsync<TokenResponse>();
        tokens!.AccessToken.Should().NotBeNullOrWhiteSpace();
        tokens.RefreshToken.Should().NotBeNullOrWhiteSpace();
        tokens.ExpiresIn.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsUnauthorized()
    {
        // Arrange
        using var factory = new QuotesApiFactory(sqlServer);
        var client = factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = SeededEmail, password = "not-the-password" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_UnknownEmail_ReturnsUnauthorized()
    {
        // Arrange
        using var factory = new QuotesApiFactory(sqlServer);
        var client = factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "nobody@example.com", password = SeededPassword });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_StoresRefreshTokenExpiryFromClock()
    {
        // Arrange
        using var factory = new QuotesApiFactory(sqlServer);
        var client = factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = SeededEmail, password = SeededPassword });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QuotesDbContext>();
        var stored = await db.RefreshTokens.SingleAsync();
        stored.ExpiresAt.Should().Be(factory.Clock.UtcNow.AddDays(RefreshTokenService.LifetimeDays));
    }

    [Fact]
    public async Task Refresh_ValidToken_ReturnsRotatedTokens()
    {
        // Arrange
        using var factory = new QuotesApiFactory(sqlServer);
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = SeededEmail, password = SeededPassword });
        var tokens = await login.Content.ReadFromJsonAsync<TokenResponse>();

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = tokens!.RefreshToken });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rotated = await response.Content.ReadFromJsonAsync<TokenResponse>();
        rotated!.RefreshToken.Should().NotBe(tokens.RefreshToken);
        rotated.AccessToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Refresh_ReusedRevokedToken_ReturnsUnauthorized()
    {
        // Arrange
        using var factory = new QuotesApiFactory(sqlServer);
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = SeededEmail, password = SeededPassword });
        var tokens = await login.Content.ReadFromJsonAsync<TokenResponse>();
        await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = tokens!.RefreshToken });

        // Act - replay the token that was already rotated away
        var response = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = tokens.RefreshToken });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_ExpiredToken_ReturnsUnauthorized()
    {
        // Arrange
        using var factory = new QuotesApiFactory(sqlServer);
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = SeededEmail, password = SeededPassword });
        var tokens = await login.Content.ReadFromJsonAsync<TokenResponse>();

        // Act - move the fake clock past the refresh-token lifetime
        factory.Clock.UtcNow = factory.Clock.UtcNow.AddDays(RefreshTokenService.LifetimeDays + 1);
        var response = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = tokens!.RefreshToken });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_MissingToken_ReturnsUnauthorized()
    {
        // Arrange
        using var factory = new QuotesApiFactory(sqlServer);
        var client = factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = (string?)null });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_ValidRefreshToken_RevokesToken()
    {
        // Arrange
        using var factory = new QuotesApiFactory(sqlServer);
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = SeededEmail, password = SeededPassword });
        var tokens = await login.Content.ReadFromJsonAsync<TokenResponse>();

        // Act
        var logout = await client.PostAsJsonAsync("/api/auth/logout", new { refreshToken = tokens!.RefreshToken });
        var refreshAfterLogout = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = tokens.RefreshToken });

        // Assert
        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);
        refreshAfterLogout.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
