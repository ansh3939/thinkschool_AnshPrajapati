using System.Net.Http.Json;
using System.Text.Json.Serialization;
using QuotesApi.Extensions;
using QuotesApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddZenQuotesClient();

var app = builder.Build();

var quotes = new List<Quote>();
var nextId = 1;

app.MapGet("/api/quotes", () => Results.Ok(quotes));

app.MapPost("/api/quotes/import", async (
    IHttpClientFactory httpClientFactory,
    ILogger<Program> logger) =>
{
    var client = httpClientFactory.CreateClient("zenquotes");
    var externalQuotes = await client.GetFromJsonAsync<ExternalQuote[]>("api/random");
    var external = externalQuotes?.FirstOrDefault();

    if (external is null || string.IsNullOrWhiteSpace(external.Text))
    {
        logger.LogWarning("External quote provider returned no content");
        return Results.Problem("Could not fetch a quote from the external provider.", statusCode: StatusCodes.Status502BadGateway);
    }

    var quote = new Quote(nextId++, $"{external.Text} — {external.Author}");
    quotes.Add(quote);

    logger.LogInformation("Imported quote {QuoteId}", quote.Id);

    return Results.Created($"/api/quotes/{quote.Id}", quote);
});

app.Run();

public partial class Program;

public record ExternalQuote(
    [property: JsonPropertyName("q")] string? Text,
    [property: JsonPropertyName("a")] string? Author);
