using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QuotesApi.Authentication;
using QuotesApi.Authorization;
using QuotesApi.Clock;
using QuotesApi.Data;
using QuotesApi.Models;

var builder = WebApplication.CreateBuilder(args);

var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()
    ?? throw new InvalidOperationException("JWT configuration is missing.");
var entraOptions = builder.Configuration.GetSection("Entra").Get<EntraOptions>()
    ?? throw new InvalidOperationException("Entra configuration is missing.");
if (string.IsNullOrWhiteSpace(entraOptions.Authority) || string.IsNullOrWhiteSpace(entraOptions.Audience))
    throw new InvalidOperationException("Entra authority and audience must be configured.");
var signingKey = JwtOptionsValidator.GetSigningKey(jwtOptions);

builder.Services.AddDbContext<QuotesDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("QuotesDb")
        ?? throw new InvalidOperationException("QuotesDb connection string is missing.")));
builder.Services.AddSingleton(jwtOptions);
builder.Services.AddSingleton(entraOptions);
// Qualified because Microsoft.AspNetCore.Authentication also has a SystemClock.
builder.Services.AddSingleton<IClock, QuotesApi.Clock.SystemClock>();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddSingleton<RefreshTokenService>();
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = "SmartBearer";
        options.DefaultChallengeScheme = "SmartBearer";
    })
    .AddPolicyScheme("SmartBearer", "Selects an authentication scheme from the token issuer.", options =>
    {
        options.ForwardDefaultSelector = context =>
        {
            var authorizationHeader = context.Request.Headers.Authorization.ToString();
            if (authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                var token = authorizationHeader["Bearer ".Length..].Trim();
                var tokenHandler = new JwtSecurityTokenHandler();
                if (tokenHandler.CanReadToken(token) && tokenHandler.ReadJwtToken(token).Issuer == entraOptions.Authority)
                    return "EntraJwt";
            }

            return "InternalJwt";
        };
    })
    .AddJwtBearer("InternalJwt", options =>
    {
        options.IncludeErrorDetails = true;
        options.Events = new JwtBearerEvents
        {
            OnChallenge = context =>
            {
                if (!string.IsNullOrWhiteSpace(context.Request.Headers.Authorization))
                {
                    context.HandleResponse();
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.Headers.Append(
                        "WWW-Authenticate",
                        "Bearer error=\"invalid_token\", error_description=\"The access token is invalid or expired.\"");
                }

                return Task.CompletedTask;
            }
        };
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    })
    .AddJwtBearer("EntraJwt", options =>
    {
        options.Authority = entraOptions.Authority;
        options.Audience = entraOptions.Audience;
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("can-edit-quotes", policy =>
        policy.RequireClaim("scope", "quotes.write"));
    options.AddPolicy("can-delete-own-quote", policy =>
        policy.RequireAuthenticatedUser()
            .AddRequirements(new QuoteOwnerRequirement()));
});
builder.Services.AddSingleton<IAuthorizationHandler, QuoteOwnerRequirementHandler>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<QuotesDbContext>();
    await db.Database.MigrateAsync();

    if (!await db.Users.AnyAsync(user => user.Email == "test@example.com"))
    {
        db.Users.Add(new User
        {
            Email = "test@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123")
        });
    }

    if (!await db.Quotes.AnyAsync())
        db.Quotes.Add(new Quote { Text = "JWT authentication protects quote changes.", OwnerId = "1" });

    await db.SaveChangesAsync();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapPost("/api/auth/login", async (LoginRequest request, QuotesDbContext db, JwtTokenService tokens, RefreshTokenService refreshTokens, IClock clock) =>
{
    var user = await db.Users.SingleOrDefaultAsync(candidate => candidate.Email == request.Email);
    if (user is null || string.IsNullOrWhiteSpace(request.Password) || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        return Results.Unauthorized();

    var accessToken = tokens.CreateAccessToken(user);
    var refreshToken = refreshTokens.CreateToken();
    db.RefreshTokens.Add(new RefreshToken
    {
        Token = refreshTokens.HashToken(refreshToken),
        UserId = user.Id,
        ExpiresAt = clock.UtcNow.AddDays(RefreshTokenService.LifetimeDays),
        FamilyId = Guid.NewGuid()
    });
    await db.SaveChangesAsync();

    return Results.Ok(new
    {
        access_token = accessToken,
        refresh_token = refreshToken,
        expires_in = jwtOptions.AccessTokenLifetimeMinutes * 60
    });
});

app.MapPost("/api/auth/refresh", async (RefreshTokenRequest request, QuotesDbContext db, JwtTokenService tokens, RefreshTokenService refreshTokens, IClock clock, ILogger<Program> logger) =>
{
    if (string.IsNullOrWhiteSpace(request.RefreshToken))
        return Results.Unauthorized();

    var tokenHash = refreshTokens.HashToken(request.RefreshToken);
    var currentToken = await db.RefreshTokens.Include(token => token.User)
        .SingleOrDefaultAsync(token => token.Token == tokenHash);

    if (currentToken is null || currentToken.ExpiresAt <= clock.UtcNow)
        return Results.Unauthorized();

    if (currentToken.RevokedAt is not null)
    {
        if (!string.IsNullOrWhiteSpace(currentToken.ReplacedByToken))
        {
            logger.LogWarning("Refresh-token reuse detected for user {UserId}; revoking token family {FamilyId}.", currentToken.UserId, currentToken.FamilyId);
            var activeFamilyTokens = await db.RefreshTokens
                .Where(token => token.FamilyId == currentToken.FamilyId && token.RevokedAt == null)
                .ToListAsync();
            foreach (var familyToken in activeFamilyTokens)
                familyToken.RevokedAt = clock.UtcNow;
            await db.SaveChangesAsync();
        }

        return Results.Unauthorized();
    }

    var newRefreshToken = refreshTokens.CreateToken();
    var newRefreshTokenHash = refreshTokens.HashToken(newRefreshToken);
    currentToken.RevokedAt = clock.UtcNow;
    currentToken.ReplacedByToken = newRefreshTokenHash;
    db.RefreshTokens.Add(new RefreshToken
    {
        Token = newRefreshTokenHash,
        UserId = currentToken.UserId,
        ExpiresAt = clock.UtcNow.AddDays(RefreshTokenService.LifetimeDays),
        FamilyId = currentToken.FamilyId
    });
    await db.SaveChangesAsync();

    return Results.Ok(new
    {
        access_token = tokens.CreateAccessToken(currentToken.User),
        refresh_token = newRefreshToken,
        expires_in = jwtOptions.AccessTokenLifetimeMinutes * 60
    });
});

app.MapPost("/api/auth/logout", async (RefreshTokenRequest request, QuotesDbContext db, RefreshTokenService refreshTokens, IClock clock) =>
{
    if (!string.IsNullOrWhiteSpace(request.RefreshToken))
    {
        var tokenHash = refreshTokens.HashToken(request.RefreshToken);
        var token = await db.RefreshTokens.SingleOrDefaultAsync(item => item.Token == tokenHash);
        if (token is not null && token.RevokedAt is null)
        {
            token.RevokedAt = clock.UtcNow;
            await db.SaveChangesAsync();
        }
    }

    return Results.NoContent();
});

app.MapGet("/api/quotes", async (QuotesDbContext db) =>
    Results.Ok(await db.Quotes.OrderBy(quote => quote.Id).ToListAsync()));

app.MapPost("/api/quotes", async (CreateQuoteRequest request, ClaimsPrincipal user, QuotesDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(request.Text))
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["text"] = ["Text is required."] });

    var ownerId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
    if (string.IsNullOrWhiteSpace(ownerId))
        return Results.Forbid();

    var quote = new Quote { Text = request.Text.Trim(), OwnerId = ownerId };
    db.Quotes.Add(quote);
    await db.SaveChangesAsync();
    return Results.Created($"/api/quotes/{quote.Id}", quote);
}).RequireAuthorization("can-edit-quotes");

app.MapDelete("/api/quotes/{id:int}", async (
    int id,
    ClaimsPrincipal user,
    QuotesDbContext db,
    IAuthorizationService authorizationService) =>
{
    var quote = await db.Quotes.FindAsync(id);
    if (quote is null)
        return Results.NotFound();

    var authorizationResult = await authorizationService.AuthorizeAsync(user, quote, "can-delete-own-quote");
    if (!authorizationResult.Succeeded)
        return Results.Forbid();

    db.Quotes.Remove(quote);
    await db.SaveChangesAsync();
    return Results.NoContent();
}).RequireAuthorization();

app.Run();

public partial class Program;

public record LoginRequest(string? Email, string? Password);
public record RefreshTokenRequest(string? RefreshToken);
public record CreateQuoteRequest(string? Text);
