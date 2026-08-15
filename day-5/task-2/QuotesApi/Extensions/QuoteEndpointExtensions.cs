using Microsoft.AspNetCore.Mvc;
using QuotesApi.Models;
using QuotesApi.Repositories;

namespace QuotesApi.Extensions;

public record CreateQuoteRequest(
    string Author,
    string Text);

public static class QuoteEndpointExtensions
{
    public static IEndpointRouteBuilder MapQuoteEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/quotes");

        group.MapGet("/", async (
            int? page,
            int? size,
            IQuoteRepository repository,
            CancellationToken cancellationToken) =>
        {
            var currentPage = page ?? 1;
            var pageSize = size ?? 10;

            var errors = new Dictionary<string, string[]>();

            if (currentPage < 1)
                errors["page"] = ["Page must be greater than 0."];

            if (pageSize < 1 || pageSize > 100)
                errors["size"] = ["Size must be between 1 and 100."];

            if (errors.Count > 0)
            {
                var validation = new ValidationProblemDetails(errors)
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Validation failed."
                };

                return Results.Json(
                    validation,
                    statusCode: 400,
                    contentType: "application/problem+json");
            }

            var result = await repository.GetPagedAsync(
                currentPage,
                pageSize,
                cancellationToken);

            return Results.Ok(new
            {
                page = currentPage,
                size = pageSize,
                total = result.Total,
                items = result.Quotes
            });
        });

        group.MapPost("/", async (
            CreateQuoteRequest request,
            IQuoteRepository repository,
            CancellationToken cancellationToken) =>
        {
            var errors = new Dictionary<string, string[]>();

            if (string.IsNullOrWhiteSpace(request.Author))
                errors["author"] = ["Author is required."];

            if (string.IsNullOrWhiteSpace(request.Text))
                errors["text"] = ["Text is required."];

            if (errors.Count > 0)
            {
                var validation = new ValidationProblemDetails(errors)
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Validation failed."
                };

                return Results.Json(
                    validation,
                    statusCode: 400,
                    contentType: "application/problem+json");
            }

            var quote = new Quote
            {
                Author = request.Author.Trim(),
                Text = request.Text.Trim()
            };

            await repository.AddAsync(
                quote,
                cancellationToken);

            return Results.Created(
                $"/api/quotes/{quote.Id}",
                quote);
        });

        group.MapGet("/{id:int}", async (
            int id,
            IQuoteRepository repository,
            CancellationToken cancellationToken) =>
        {
            var quote = await repository.GetByIdAsync(
                id,
                cancellationToken);

            return quote is null
                ? Results.Problem(
                    statusCode: 404,
                    title: "Quote not found.",
                    detail: $"Quote with id {id} was not found.")
                : Results.Ok(quote);
        });

        group.MapDelete("/{id:int}", async (
            int id,
            IQuoteRepository repository,
            CancellationToken cancellationToken) =>
        {
            var deleted = await repository.DeleteAsync(
                id,
                cancellationToken);

            return deleted
                ? Results.NoContent()
                : Results.Problem(
                    statusCode: 404,
                    title: "Quote not found.",
                    detail: $"Quote with id {id} was not found.");
        });

        return app;
    }
}