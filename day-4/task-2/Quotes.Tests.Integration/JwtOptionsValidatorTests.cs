using FluentAssertions;
using Microsoft.IdentityModel.Tokens;
using QuotesApi.Authentication;

namespace Quotes.Tests.Integration;

/// <summary>
/// GetSigningKey runs once at app startup, before any HTTP request exists, so the
/// WebApplicationFactory-based tests only ever exercise the valid-config path. These
/// unit tests cover the misconfiguration branches directly.
/// </summary>
public sealed class JwtOptionsValidatorTests
{
    private const string ValidKey = "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=";

    [Fact]
    public void GetSigningKey_ValidOptions_ReturnsKey()
    {
        // Arrange
        var options = new JwtOptions { Key = ValidKey, Issuer = "QuotesApi", Audience = "QuotesApi" };

        // Act
        var key = JwtOptionsValidator.GetSigningKey(options);

        // Assert
        key.Should().BeOfType<SymmetricSecurityKey>();
        key.KeyId.Should().BeNull();
    }

    [Fact]
    public void GetSigningKey_MissingIssuer_Throws()
    {
        // Arrange
        var options = new JwtOptions { Key = ValidKey, Issuer = null, Audience = "QuotesApi" };

        // Act
        var act = () => JwtOptionsValidator.GetSigningKey(options);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*issuer and audience*");
    }

    [Fact]
    public void GetSigningKey_MissingAudience_Throws()
    {
        // Arrange
        var options = new JwtOptions { Key = ValidKey, Issuer = "QuotesApi", Audience = "   " };

        // Act
        var act = () => JwtOptionsValidator.GetSigningKey(options);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*issuer and audience*");
    }

    [Fact]
    public void GetSigningKey_KeyNotValidBase64_Throws()
    {
        // Arrange
        var options = new JwtOptions { Key = "not-base64!!", Issuer = "QuotesApi", Audience = "QuotesApi" };

        // Act
        var act = () => JwtOptionsValidator.GetSigningKey(options);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*valid Base64*")
            .WithInnerException<FormatException>();
    }

    [Fact]
    public void GetSigningKey_KeyNotConfigured_Throws()
    {
        // Arrange - Key was never set, e.g. missing from appsettings/secrets
        var options = new JwtOptions { Key = null, Issuer = "QuotesApi", Audience = "QuotesApi" };

        // Act
        var act = () => JwtOptionsValidator.GetSigningKey(options);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*32-byte*");
    }

    [Fact]
    public void GetSigningKey_KeyWrongLength_Throws()
    {
        // Arrange - valid Base64, but only 16 bytes instead of the required 32
        var options = new JwtOptions { Key = Convert.ToBase64String(new byte[16]), Issuer = "QuotesApi", Audience = "QuotesApi" };

        // Act
        var act = () => JwtOptionsValidator.GetSigningKey(options);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*32-byte*");
    }
}
