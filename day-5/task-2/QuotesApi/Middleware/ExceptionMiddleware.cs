using System.Net;
using Microsoft.AspNetCore.Mvc;

namespace QuotesApi.Middleware;

public class ExceptionMiddleware(
    RequestDelegate next,
    ILogger<ExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Unhandled exception while processing {Method} {Path}",
                context.Request.Method,
                context.Request.Path);

            context.Response.StatusCode =
                (int)HttpStatusCode.InternalServerError;

            context.Response.ContentType = "application/problem+json";

            var problem = new ProblemDetails
            {
                Status = 500,
                Title = "An unexpected error occurred.",
                Detail = "The server encountered an unexpected error."
            };

            await context.Response.WriteAsJsonAsync(problem);
        }
    }
}