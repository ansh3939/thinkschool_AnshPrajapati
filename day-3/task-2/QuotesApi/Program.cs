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

var builder = WebApplication.CreateBuilder(args);

var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()
    ?? throw new InvalidOperationException("JWT configuration is missing.");
var signingKey = JwtOptionsValidator.GetSigningKey(jwtOptions);

builder.Services.AddDbContext<QuotesDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("QuotesDb") ?? "Data Source=quotes.db"));
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

    if (!await db.Quotes.AnyAsync())
        db.Quotes.Add(new Quote { Text = "Authorization policies protect quote changes.", OwnerId = "seed-user" });

    await db.SaveChangesAsync();
}

app.UseAuthentication();
app.UseAuthorization();

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

public record CreateQuoteRequest(string? Text);
