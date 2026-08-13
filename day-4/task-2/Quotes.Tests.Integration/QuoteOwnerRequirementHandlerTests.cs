using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using QuotesApi.Authorization;
using QuotesApi.Models;

namespace Quotes.Tests.Integration;

/// <summary>
/// The integration tests only ever exercise tokens carrying a NameIdentifier claim, so the
/// Sub-claim fallback and the no-claim-at-all path never run. These unit tests drive the
/// handler directly with crafted principals to cover the rest of its claim-resolution logic.
/// </summary>
public sealed class QuoteOwnerRequirementHandlerTests
{
    private readonly QuoteOwnerRequirementHandler _handler = new();
    private readonly Quote _quote = new() { Id = 1, Text = "Ours or theirs?", OwnerId = "user-1" };

    [Fact]
    public async Task HandleRequirementAsync_NameIdentifierMatches_Succeeds()
    {
        // Arrange
        var user = PrincipalWith(new Claim(ClaimTypes.NameIdentifier, "user-1"));
        var context = new AuthorizationHandlerContext([new QuoteOwnerRequirement()], user, _quote);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task HandleRequirementAsync_NoNameIdentifierFallsBackToSubClaim_Succeeds()
    {
        // Arrange - Entra-issued tokens carry "sub" but no NameIdentifier claim
        var user = PrincipalWith(new Claim(JwtRegisteredClaimNames.Sub, "user-1"));
        var context = new AuthorizationHandlerContext([new QuoteOwnerRequirement()], user, _quote);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task HandleRequirementAsync_NoOwnerClaimAtAll_DoesNotSucceed()
    {
        // Arrange - authenticated principal with neither NameIdentifier nor Sub
        var user = PrincipalWith(new Claim(ClaimTypes.Email, "someone@example.com"));
        var context = new AuthorizationHandlerContext([new QuoteOwnerRequirement()], user, _quote);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task HandleRequirementAsync_ClaimBelongsToDifferentUser_DoesNotSucceed()
    {
        // Arrange
        var user = PrincipalWith(new Claim(ClaimTypes.NameIdentifier, "someone-else"));
        var context = new AuthorizationHandlerContext([new QuoteOwnerRequirement()], user, _quote);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
    }

    private static ClaimsPrincipal PrincipalWith(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, authenticationType: "TestAuth"));
}
