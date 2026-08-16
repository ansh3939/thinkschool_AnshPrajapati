using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace QuotesApi.Extensions;

public static class HttpClientExtensions
{
    // zenquotes.io is a third-party API - it can be slow, rate-limit us, or just be
    // down. Wrap it in the same resilience pipeline you'd put around any outbound
    // call (Entra ID, a partner API, ...): retry transient failures, stop hammering
    // it once it's clearly unhealthy, and never let one call hang the request forever.
    public static void AddZenQuotesClient(this IServiceCollection services) =>
        services.AddHttpClient("zenquotes", client =>
        {
            client.BaseAddress = new Uri("https://zenquotes.io/");
            client.Timeout = TimeSpan.FromSeconds(10);
        })
        .AddResilienceHandler("default", (pipeline, context) =>
        {
            var logger = context.ServiceProvider.GetRequiredService<ILoggerFactory>()
                .CreateLogger("QuotesApi.Resilience.ZenQuotes");

            pipeline.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                OnRetry = args =>
                {
                    logger.LogWarning(
                        args.Outcome.Exception,
                        "Retry {AttemptNumber} calling {Method} {Uri} after {DelayMs}ms (status: {StatusCode})",
                        args.AttemptNumber + 1,
                        args.Outcome.Result?.RequestMessage?.Method,
                        args.Outcome.Result?.RequestMessage?.RequestUri,
                        args.RetryDelay.TotalMilliseconds,
                        args.Outcome.Result?.StatusCode);
                    return default;
                }
            });

            // Opens once 50% of calls fail within a rolling 30s window, so we stop
            // sending doomed requests at a dead upstream instead of retrying forever.
            pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = 10,
                BreakDuration = TimeSpan.FromSeconds(30)
            });

            pipeline.AddTimeout(TimeSpan.FromSeconds(10));
        });
}
