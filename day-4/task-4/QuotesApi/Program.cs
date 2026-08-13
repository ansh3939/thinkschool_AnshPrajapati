using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QuotesApi.Authentication;
using QuotesApi.Authorization;
using QuotesApi.Data;
using QuotesApi.Models;
using Serilog;
using Serilog.Context;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"));

var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()
    ?? throw new InvalidOperationException("JWT configuration is missing.");
var signingKey = JwtOptionsValidator.GetSigningKey(jwtOptions);

builder.Services.AddDbContext<QuotesDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("QuotesDb") ?? "Data Source=quotes.db"));
builder.Services.AddSingleton(jwtOptions);
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
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
        db.Quotes.Add(new Quote { Text = "Structured logging makes production issues traceable.", OwnerId = "1" });

    await db.SaveChangesAsync();
}

app.Use(async (ctx, next) =>
{
    using (LogContext.PushProperty("TraceId", ctx.TraceIdentifier))
    {
        await next();
    }
});

app.UseSerilogRequestLogging();

app.UseAuthentication();
app.UseAuthorization();

app.MapPost("/api/auth/login", async (LoginRequest request, QuotesDbContext db, JwtTokenService tokens, ILogger<Program> logger) =>
{
    var user = await db.Users.SingleOrDefaultAsync(candidate => candidate.Email == request.Email);
    if (user is null || string.IsNullOrWhiteSpace(request.Password) || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
    {
        logger.LogWarning("Login failed for {Email}", request.Email);
        return Results.Unauthorized();
    }

    logger.LogInformation("User {UserId} logged in", user.Id);
    return Results.Ok(new { access_token = tokens.CreateAccessToken(user) });
});

app.MapGet("/api/quotes", async (QuotesDbContext db) =>
    Results.Ok(await db.Quotes.OrderBy(quote => quote.Id).ToListAsync()));

app.MapPost("/api/quotes", async (CreateQuoteRequest request, ClaimsPrincipal user, QuotesDbContext db, ILogger<Program> logger) =>
{
    var ownerId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
    logger.LogInformation("Received create-quote request for owner {OwnerId}", ownerId);

    if (string.IsNullOrWhiteSpace(request.Text))
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["text"] = ["Text is required."] });

    if (string.IsNullOrWhiteSpace(ownerId))
        return Results.Forbid();

    var quote = new Quote { Text = request.Text.Trim(), OwnerId = ownerId };
    db.Quotes.Add(quote);
    await db.SaveChangesAsync();

    logger.LogInformation("Created quote {QuoteId} for user {UserId}", quote.Id, ownerId);

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
public record CreateQuoteRequest(string? Text);
