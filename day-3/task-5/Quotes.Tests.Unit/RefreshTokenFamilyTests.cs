using FluentAssertions;
using NSubstitute;
using Quotes;

namespace Quotes.Tests.Unit;

public sealed class RefreshTokenFamilyTests
{
    [Fact]
    public void Create_ValidInput_CreatesActiveToken()
    {
        // Arrange
        var now = new DateTime(2026, 8, 12, 10, 0, 0, DateTimeKind.Utc);
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(now);

        // Act
        var family = RefreshTokenFamily.Create("user-1", "token-1", clock);

        // Assert
        family.UserId.Should().Be("user-1");
        family.ActiveToken.Token.Should().Be("token-1");
        family.ActiveToken.ExpiresAt.Should().Be(now.AddDays(RefreshTokenFamily.LifetimeDays));
        family.ActiveToken.RevokedAt.Should().BeNull();
    }

    [Fact]
    public void Rotate_ActiveToken_ReturnsNewToken()
    {
        // Arrange
        var now = new DateTime(2026, 8, 12, 10, 0, 0, DateTimeKind.Utc);
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(now);
        var family = RefreshTokenFamily.Create("user-1", "token-1", clock);

        // Act
        var result = family.Rotate("token-1", "token-2");

        // Assert
        result.Kind.Should().Be(RefreshResultKind.Rotated);
        result.NewToken.Should().Be("token-2");
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Rotate_ActiveToken_RevokesOldToken()
    {
        // Arrange
        var now = new DateTime(2026, 8, 12, 10, 0, 0, DateTimeKind.Utc);
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(now);
        var family = RefreshTokenFamily.Create("user-1", "token-1", clock);

        // Act
        family.Rotate("token-1", "token-2");

        // Assert
        var oldToken = family.Tokens.Single(token => token.Token == "token-1");
        oldToken.RevokedAt.Should().Be(now);
        oldToken.ReplacedByToken.Should().Be("token-2");
    }

    [Fact]
    public void Rotate_ActiveToken_AddsReplacementToken()
    {
        // Arrange
        var now = new DateTime(2026, 8, 12, 10, 0, 0, DateTimeKind.Utc);
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(now);
        var family = RefreshTokenFamily.Create("user-1", "token-1", clock);

        // Act
        family.Rotate("token-1", "token-2");

        // Assert
        family.ActiveToken.Token.Should().Be("token-2");
        family.ActiveToken.ExpiresAt.Should().Be(now.AddDays(RefreshTokenFamily.LifetimeDays));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rotate_MissingToken_ReturnsMissingToken(string? token)
    {
        // Arrange
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(new DateTime(2026, 8, 12, 10, 0, 0, DateTimeKind.Utc));
        var family = RefreshTokenFamily.Create("user-1", "token-1", clock);

        // Act
        var result = family.Rotate(token, "token-2");

        // Assert
        result.Kind.Should().Be(RefreshResultKind.MissingToken);
        result.Succeeded.Should().BeFalse();
        family.ActiveToken.Token.Should().Be("token-1");
    }

    [Fact]
    public void Rotate_UnknownToken_ReturnsUnknownToken()
    {
        // Arrange
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(new DateTime(2026, 8, 12, 10, 0, 0, DateTimeKind.Utc));
        var family = RefreshTokenFamily.Create("user-1", "token-1", clock);

        // Act
        var result = family.Rotate("wrong-token", "token-2");

        // Assert
        result.Kind.Should().Be(RefreshResultKind.UnknownToken);
        result.NewToken.Should().BeNull();
        family.ActiveToken.Token.Should().Be("token-1");
    }

    [Fact]
    public void Rotate_ExpiredToken_ReturnsExpiredToken()
    {
        // Arrange
        var issuedAt = new DateTime(2026, 8, 12, 10, 0, 0, DateTimeKind.Utc);
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(issuedAt);
        var family = RefreshTokenFamily.Create("user-1", "token-1", clock);
        clock.UtcNow.Returns(issuedAt.AddDays(RefreshTokenFamily.LifetimeDays).AddTicks(1));

        // Act
        var result = family.Rotate("token-1", "token-2");

        // Assert
        result.Kind.Should().Be(RefreshResultKind.ExpiredToken);
        result.NewToken.Should().BeNull();
        family.ActiveToken.Token.Should().Be("token-1");
    }

    [Fact]
    public void Rotate_RevokedTokenWithoutReplacement_ReturnsRevokedToken()
    {
        // Arrange
        var now = new DateTime(2026, 8, 12, 10, 0, 0, DateTimeKind.Utc);
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(now);
        var family = RefreshTokenFamily.Create("user-1", "token-1", clock);
        family.ActiveToken.RevokedAt = now;

        // Act
        var result = family.Rotate("token-1", "token-2");

        // Assert
        result.Kind.Should().Be(RefreshResultKind.RevokedToken);
        result.NewToken.Should().BeNull();
    }

    [Fact]
    public void Rotate_ReusedReplacementToken_ReturnsReuseDetected()
    {
        // Arrange
        var now = new DateTime(2026, 8, 12, 10, 0, 0, DateTimeKind.Utc);
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(now);
        var family = RefreshTokenFamily.Create("user-1", "token-1", clock);
        family.Rotate("token-1", "token-2");

        // Act
        var result = family.Rotate("token-1", "token-3");

        // Assert
        result.Kind.Should().Be(RefreshResultKind.ReuseDetected);
        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public void Rotate_ReusedReplacementToken_RevokesActiveFamilyTokens()
    {
        // Arrange
        var now = new DateTime(2026, 8, 12, 10, 0, 0, DateTimeKind.Utc);
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(now);
        var family = RefreshTokenFamily.Create("user-1", "token-1", clock);
        family.Rotate("token-1", "token-2");

        // Act
        family.Rotate("token-1", "token-3");

        // Assert
        family.Tokens.Should().OnlyContain(token => token.RevokedAt == now);
        family.Tokens.Should().NotContain(token => token.Token == "token-3");
    }
}
