using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;
using QuotesApi.Extensions;
using QuotesApi.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);

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