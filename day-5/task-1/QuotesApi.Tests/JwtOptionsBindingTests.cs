using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using QuotesApi.Authentication;
using QuotesApi.Models;

namespace QuotesApi.Tests;

/// <summary>
/// Covers the two things the "Jwt" config section is supposed to do: bind onto
/// <see cref="JwtOptions"/> the same way <c>services.Configure&lt;JwtOptions&gt;</c> does in
/// Program.cs, and let a higher-precedence source (environment variables) override a
/// lower one (appsettings-equivalent), matching the real
/// environment variables &gt; appsettings.{Environment}.json &gt; appsettings.json order.
/// </summary>
public sealed class JwtOptionsBindingTests
{
    private static readonly Dictionary<string, string?> AppSettingsEquivalent = new()
    {
        ["Jwt:Issuer"] = "QuotesApi",
        ["Jwt:Audience"] = "QuotesApi",
        ["Jwt:AccessTokenLifetime"] = "00:15:00"
    };

    [Fact]
    public void JwtOptions_BindsFromConfigurationSection()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(AppSettingsEquivalent)
            .Build();

        var options = configuration.GetSection("Jwt").Get<JwtOptions>();

        options.Should().NotBeNull();
        options!.Issuer.Should().Be("QuotesApi");
        options.Audience.Should().Be("QuotesApi");
        options.AccessTokenLifetime.Should().Be(TimeSpan.FromMinutes(15));
    }

    [Fact]
    public void EnvironmentVariable_OverridesAppSettingsValue()
    {
        // "Jwt__Audience" is how ASP.NET Core's environment variable provider spells the
        // nested key "Jwt:Audience" (colons aren't valid in most shells/OS env vars).
        Environment.SetEnvironmentVariable("Jwt__Audience", "QuotesApi.Prod");
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(AppSettingsEquivalent) // lowest precedence, added first
                .AddEnvironmentVariables()                    // highest precedence, added last
                .Build();

            var options = configuration.GetSection("Jwt").Get<JwtOptions>();

            options!.Audience.Should().Be("QuotesApi.Prod");
        }
        finally
        {
            Environment.SetEnvironmentVariable("Jwt__Audience", null);
        }
    }

    [Fact]
    public void JwtTokenService_UsesOptionsInjectedThroughIOptions()
    {
        var jwtOptions = new JwtOptions
        {
            SigningKey = "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=",
            Issuer = "QuotesApi",
            Audience = "QuotesApi",
            AccessTokenLifetime = TimeSpan.FromMinutes(5)
        };
        var tokenService = new JwtTokenService(Options.Create(jwtOptions));
        var user = new User { Id = 1, Email = "test@example.com", PasswordHash = "hash" };

        var token = tokenService.CreateAccessToken(user);

        token.Should().NotBeNullOrWhiteSpace();
        var payload = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(token);
        payload.Issuer.Should().Be(jwtOptions.Issuer);
        payload.Audiences.Should().Contain(jwtOptions.Audience);
    }
}
