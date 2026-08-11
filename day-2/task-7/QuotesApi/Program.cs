using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QuotesApi.Authentication;
using QuotesApi.Data;
using QuotesApi.Models;

var builder = WebApplication.CreateBuilder(args);

var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()
    ?? throw new InvalidOperationException("JWT configuration is missing.");
var signingKey = JwtOptionsValidator.GetSigningKey(jwtOptions);

builder.Services.AddDbContext<QuotesDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("QuotesDb") ?? "Data Source=quotes.db"));
builder.Services.AddSingleton(jwtOptions);
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddSingleton<RefreshTokenService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
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
    });
builder.Services.AddAuthorization();

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
        db.Quotes.Add(new Quote { Text = "JWT authentication protects quote changes." });

    await db.SaveChangesAsync();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapPost("/api/auth/login", async (LoginRequest request, QuotesDbContext db, JwtTokenService tokens, RefreshTokenService refreshTokens) =>
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
        ExpiresAt = DateTime.UtcNow.AddDays(RefreshTokenService.LifetimeDays),
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

app.MapPost("/api/auth/refresh", async (RefreshTokenRequest request, QuotesDbContext db, JwtTokenService tokens, RefreshTokenService refreshTokens, ILogger<Program> logger) =>
{
    if (string.IsNullOrWhiteSpace(request.RefreshToken))
        return Results.Unauthorized();

    var tokenHash = refreshTokens.HashToken(request.RefreshToken);
    var currentToken = await db.RefreshTokens.Include(token => token.User)
        .SingleOrDefaultAsync(token => token.Token == tokenHash);

    if (currentToken is null || currentToken.ExpiresAt <= DateTime.UtcNow)
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
                familyToken.RevokedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        return Results.Unauthorized();
    }

    var newRefreshToken = refreshTokens.CreateToken();
    var newRefreshTokenHash = refreshTokens.HashToken(newRefreshToken);
    currentToken.RevokedAt = DateTime.UtcNow;
    currentToken.ReplacedByToken = newRefreshTokenHash;
    db.RefreshTokens.Add(new RefreshToken
    {
        Token = newRefreshTokenHash,
        UserId = currentToken.UserId,
        ExpiresAt = DateTime.UtcNow.AddDays(RefreshTokenService.LifetimeDays),
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

app.MapPost("/api/auth/logout", async (RefreshTokenRequest request, QuotesDbContext db, RefreshTokenService refreshTokens) =>
{
    if (!string.IsNullOrWhiteSpace(request.RefreshToken))
    {
        var tokenHash = refreshTokens.HashToken(request.RefreshToken);
        var token = await db.RefreshTokens.SingleOrDefaultAsync(item => item.Token == tokenHash);
        if (token is not null && token.RevokedAt is null)
        {
            token.RevokedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
    }

    return Results.NoContent();
});

app.MapGet("/api/quotes", async (QuotesDbContext db) =>
    Results.Ok(await db.Quotes.OrderBy(quote => quote.Id).ToListAsync()));

app.MapPost("/api/quotes", async (CreateQuoteRequest request, QuotesDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(request.Text))
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["text"] = ["Text is required."] });

    var quote = new Quote { Text = request.Text.Trim() };
    db.Quotes.Add(quote);
    await db.SaveChangesAsync();
    return Results.Created($"/api/quotes/{quote.Id}", quote);
}).RequireAuthorization();

app.MapDelete("/api/quotes/{id:int}", async (int id, QuotesDbContext db) =>
{
    var quote = await db.Quotes.FindAsync(id);
    if (quote is null)
        return Results.NotFound();

    db.Quotes.Remove(quote);
    await db.SaveChangesAsync();
    return Results.NoContent();
}).RequireAuthorization();

app.Run();

public partial class Program;

public record LoginRequest(string? Email, string? Password);
public record RefreshTokenRequest(string? RefreshToken);
public record CreateQuoteRequest(string? Text);
