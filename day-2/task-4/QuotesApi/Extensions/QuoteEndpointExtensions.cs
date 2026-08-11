using Microsoft.AspNetCore.Mvc;
using QuotesApi.Models;
using QuotesApi.Repositories;

namespace QuotesApi.Extensions;

public record CreateQuoteRequest(string? Author, string? Text);

public static class QuoteEndpointExtensions
{
    public static IEndpointRouteBuilder MapQuoteEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/quotes");

        group.MapGet("/", async (int? page, int? size, IQuoteRepository repository, CancellationToken cancellationToken) =>
        {
            var currentPage = page ?? 1;
            var pageSize = size ?? 10;
            var errors = new Dictionary<string, string[]>();

            if (currentPage < 1)
                errors["page"] = ["Page must be greater than 0."];

            if (pageSize < 1 || pageSize > 100)
                errors["size"] = ["Size must be between 1 and 100."];

            if (errors.Count > 0)
                return Results.ValidationProblem(errors);

            var result = await repository.GetPagedAsync(currentPage, pageSize, cancellationToken);
            return Results.Ok(new
            {
                page = currentPage,
                size = pageSize,
                total = result.Total,
                items = result.Quotes
            });
        });

        group.MapPost("/", async (CreateQuoteRequest request, IQuoteRepository repository, CancellationToken cancellationToken) =>
        {
            var creation = Quote.Create(request.Author, request.Text);
            if (!creation.IsSuccess)
            {
                var errors = new Dictionary<string, string[]>
                {
                    [creation.ErrorField!] = [creation.Error!]
                };

                return Results.ValidationProblem(errors);
            }

            var quote = creation.Quote!;
            await repository.AddAsync(quote, cancellationToken);
            return Results.Created($"/api/quotes/{quote.Id}", quote);
        });

        group.MapGet("/{id:int}", async (int id, IQuoteRepository repository, CancellationToken cancellationToken) =>
        {
            var quote = await repository.GetByIdAsync(id, cancellationToken);
            return quote is null ? Results.NotFound() : Results.Ok(quote);
        });

        group.MapDelete("/{id:int}", async (int id, IQuoteRepository repository, CancellationToken cancellationToken) =>
        {
            var deleted = await repository.DeleteAsync(id, cancellationToken);
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        return app;
    }
}
