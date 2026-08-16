# Day 5 — Task 6: Add Polly resilience to HTTP calls

A minimal `QuotesApi` with one endpoint (`POST /api/quotes/import`) that calls
`zenquotes.io` for a random quote. The outbound call is wrapped in a Polly
resilience pipeline via `Microsoft.Extensions.Http.Resilience`.

## Where it's configured

`QuotesApi/Extensions/HttpClientExtensions.cs` — `AddZenQuotesClient()`, registered
once from `Program.cs`:

```csharp
services.AddHttpClient("zenquotes", client =>
{
    client.BaseAddress = new Uri("https://zenquotes.io/");
    client.Timeout = TimeSpan.FromSeconds(10);
})
.AddResilienceHandler("default", (pipeline, context) =>
{
    pipeline.AddRetry(new HttpRetryStrategyOptions { ... });
    pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions { ... });
    pipeline.AddTimeout(TimeSpan.FromSeconds(10));
});
```

## Settings

- **Retry**: `MaxRetryAttempts = 3`, exponential backoff, jitter enabled.
  `ShouldHandle` is left at the default, which already treats 5xx/408 and
  connection-level failures as transient (not 4xx client errors). `OnRetry` logs
  a warning with the attempt number, the request, the delay, and the
  status/exception that triggered the retry.
- **Circuit breaker**: opens once `FailureRatio` hits 50% over a 30-second
  `SamplingDuration`, with `MinimumThroughput = 10` and a 30-second `BreakDuration`.
- **Timeout**: `AddTimeout(TimeSpan.FromSeconds(10))` bounds each attempt.

If all 3 retries are exhausted, `GetFromJsonAsync` throws and that exception is
not caught in the `/api/quotes/import` handler — it propagates to ASP.NET Core's
default error handling (a 500 response), so a final failure is never swallowed.

## The test

`QuotesApi.Tests/ZenQuotesResilienceTests.cs` registers the same
`AddZenQuotesClient()` used by `Program.cs`, then swaps in a fake handler that
returns `503` on the first two calls and `200` on the third. A small in-memory
`ILoggerProvider` captures everything logged during the call.

It asserts:

1. The final response is `200 OK`.
2. The fake handler was called exactly 3 times (initial attempt + 2 retries).
3. Exactly 2 `"Retry ..."` log lines were captured, one per failed attempt.

That's the `503 → retry → 503 → retry → 200` sequence, driven by the real
resilience pipeline rather than a re-implementation of it. The retry delays are
real (exponential backoff with jitter isn't shortened for the test), so this
test takes a few seconds — that's expected.

## Running it

```bash
cd QuotesApi.Tests
dotnet test --filter ZenQuotesResilienceTests
```

or `dotnet test` from this folder to run the whole suite.
