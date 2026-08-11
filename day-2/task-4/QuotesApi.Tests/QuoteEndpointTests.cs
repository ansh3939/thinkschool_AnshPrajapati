using System.Net;
using System.Net.Http.Json;
using QuotesApi.Extensions;

namespace QuotesApi.Tests;

public class QuoteEndpointTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task Post_with_an_invalid_author_returns_a_domain_validation_error()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/quotes/", new CreateQuoteRequest("", "Valid text"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>();
        Assert.NotNull(problem);
        Assert.True(problem!.Errors.ContainsKey("author"));
    }

    private sealed record ValidationProblemResponse(Dictionary<string, string[]> Errors);
}
