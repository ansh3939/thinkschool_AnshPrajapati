using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using QuotesApi.Authentication;
using QuotesApi.Authorization;
using QuotesApi.Data;
using QuotesApi.Models;
using Serilog;
using Serilog.Context;

var builder = WebApplication.CreateBuilder(args);

// writeToProviders lets ILogger calls also reach the OpenTelemetry log exporter below,
// so they show up in the App Insights "traces" table alongside Serilog's console output.
builder.Host.UseSerilog((context, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"),
    writeToProviders: true);

var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()
    ?? throw new InvalidOperationException("JWT configuration is missing.");
var signingKey = JwtOptionsValidator.GetSigningKey(jwtOptions);

// Custom ActivitySource for spans that automatic instrumentation doesn't cover.
var activitySource = new ActivitySource("QuotesApi");
var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"] ?? "http://localhost:4317";

// Connection string comes from config/environment only - Azure App Service supplies it via
// a Key Vault-referenced app setting. Never hardcode it here.
var appInsightsConnectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];

var otel = builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("QuotesApi"))
    .WithTracing(tracing => tracing
        .AddSource(activitySource.Name)
        .AddAspNetCoreInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(otlp => otlp.Endpoint = new Uri(otlpEndpoint)))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(otlp => otlp.Endpoint = new Uri(otlpEndpoint)));

if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
{
    // Adds the Azure Monitor exporters for traces, metrics and logs on top of the
    // pipeline above, so local OTLP export keeps working whether or not this is set.
    otel.UseAzureMonitor(options => options.ConnectionString = appInsightsConnectionString);
}

builder.Services.AddDbContext<QuotesDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("QuotesDb") ?? "Data Source=quotes.db"));
builder.Services.AddSingleton(jwtOptions);
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddHttpClient("zenquotes", client =>
{
    client.BaseAddress = new Uri("https://zenquotes.io/");
    client.Timeout = TimeSpan.FromSeconds(10);
});
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
    // Correlate Serilog output with the OpenTelemetry trace for this request.
    using (LogContext.PushProperty("TraceId", Activity.Current?.TraceId.ToString()))
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
    if (user is null)
    {
        logger.LogWarning("Login failed for {Email}", request.Email);
        return Results.Unauthorized();
    }

    // Password verification is deliberately CPU-heavy (bcrypt) and isn't covered by
    // any automatic instrumentation, so it gets its own span.
    using var verifyActivity = activitySource.StartActivity("verify-password");
    verifyActivity?.SetTag("user.id", user.Id);
    var passwordValid = !string.IsNullOrWhiteSpace(request.Password)
        && BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);

    if (!passwordValid)
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

app.MapPost("/api/quotes/import", async (
    IHttpClientFactory httpClientFactory,
    ClaimsPrincipal user,
    QuotesDbContext db,
    ILogger<Program> logger) =>
{
    var ownerId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

    if (string.IsNullOrWhiteSpace(ownerId))
        return Results.Forbid();

    var client = httpClientFactory.CreateClient("zenquotes");
    var externalQuotes = await client.GetFromJsonAsync<ExternalQuote[]>("api/random");
    var external = externalQuotes?.FirstOrDefault();

    if (external is null || string.IsNullOrWhiteSpace(external.Text))
    {
        logger.LogWarning("External quote provider returned no content");
        return Results.Problem("Could not fetch a quote from the external provider.", statusCode: StatusCodes.Status502BadGateway);
    }

    var quote = new Quote { Text = $"{external.Text} — {external.Author}", OwnerId = ownerId };
    db.Quotes.Add(quote);
    await db.SaveChangesAsync();

    logger.LogInformation("Imported quote {QuoteId} for user {UserId}", quote.Id, ownerId);

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

public record ExternalQuote(
    [property: JsonPropertyName("q")] string? Text,
    [property: JsonPropertyName("a")] string? Author);
