using FluentAssertions;
using Microsoft.IdentityModel.Tokens;
using QuotesApi.Authentication;

namespace QuotesApi.Tests;

/// <summary>
/// GetSigningKey runs once at app startup, before any HTTP request exists, so a running
/// instance only ever exercises the valid-config path. These unit tests cover the
/// misconfiguration branches directly.
/// </summary>
public sealed class JwtOptionsValidatorTests
{
    private const string ValidKey = "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=";

    [Fact]
    public void GetSigningKey_ValidOptions_ReturnsKey()
    {
        var options = new JwtOptions { SigningKey = ValidKey, Issuer = "QuotesApi", Audience = "QuotesApi" };

        var key = JwtOptionsValidator.GetSigningKey(options);

        key.Should().BeOfType<SymmetricSecurityKey>();
    }

    [Fact]
    public void GetSigningKey_MissingIssuer_Throws()
    {
        var options = new JwtOptions { SigningKey = ValidKey, Issuer = null, Audience = "QuotesApi" };

        var act = () => JwtOptionsValidator.GetSigningKey(options);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*issuer and audience*");
    }

    [Fact]
    public void GetSigningKey_KeyNotConfigured_Throws()
    {
        // SigningKey was never set - e.g. missing from user secrets/environment.
        var options = new JwtOptions { SigningKey = null, Issuer = "QuotesApi", Audience = "QuotesApi" };

        var act = () => JwtOptionsValidator.GetSigningKey(options);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*32-byte*");
    }

    [Fact]
    public void GetSigningKey_KeyWrongLength_Throws()
    {
        // Valid Base64, but only 16 bytes instead of the required 32.
        var options = new JwtOptions { SigningKey = Convert.ToBase64String(new byte[16]), Issuer = "QuotesApi", Audience = "QuotesApi" };

        var act = () => JwtOptionsValidator.GetSigningKey(options);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*32-byte*");
    }
}
