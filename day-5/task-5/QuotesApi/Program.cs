using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using QuotesApi.Data;
using QuotesApi.Extensions;
using QuotesApi.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);

// Connection string comes from config/environment only - the container app's Bicep
// (infra/resources.bicep) injects it from the Application Insights resource it
// provisions. Never hardcode it here.
var appInsightsConnectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"];

var otel = builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("QuotesApi"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddEntityFrameworkCoreInstrumentation())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation());

// Local dev: export to an OTLP collector (e.g. Jaeger) if one is configured.
if (!string.IsNullOrWhiteSpace(otlpEndpoint))
{
    otel.WithTracing(tracing => tracing.AddOtlpExporter(otlp => otlp.Endpoint = new Uri(otlpEndpoint)))
        .WithMetrics(metrics => metrics.AddOtlpExporter(otlp => otlp.Endpoint = new Uri(otlpEndpoint)));
}

// Deployed: export to Application Insights if a connection string is present.
if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
{
    otel.UseAzureMonitor(options => options.ConnectionString = appInsightsConnectionString);
}

var app = builder.Build();

app.UseMiddleware<ExceptionMiddleware>();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<QuotesDbContext>();

    await db.Database.MigrateAsync(
        app.Lifetime.ApplicationStopping);
}

app.MapGet("/health", async (QuotesDbContext db) =>
    await db.Database.CanConnectAsync()
        ? Results.Ok(new { status = "healthy" })
        : Results.Problem("Database is unreachable.", statusCode: StatusCodes.Status503ServiceUnavailable));

app.MapQuoteEndpoints();

app.Run();