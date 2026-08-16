using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QuotesApi.Extensions;

namespace QuotesApi.Tests;

/// <summary>
/// Wires up the exact same "zenquotes" client + resilience pipeline that Program.cs
/// registers, but swaps the real network transport for a fake handler that fails
/// twice before succeeding, so the retry behavior can be verified without hitting
/// zenquotes.io.
/// </summary>
public sealed class ZenQuotesResilienceTests
{
    [Fact]
    public async Task GetAsync_RetriesTransientFailures_ThenSucceeds()
    {
        var transport = new TransientFailureThenSuccessHandler(failuresBeforeSuccess: 2);
        var logs = new ListLoggerProvider();

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(logs));
        services.AddZenQuotesClient();
        services.AddHttpClient("zenquotes").ConfigurePrimaryHttpMessageHandler(() => transport);

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("zenquotes");

        var response = await client.GetAsync("api/random");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        transport.CallCount.Should().Be(3, "the first two calls should fail transiently and the third should succeed");

        var retryLogs = logs.Messages.Where(m => m.StartsWith("Retry ")).ToList();
        retryLogs.Should().HaveCount(2, "a retry log should be produced for each of the two failed attempts");
        retryLogs[0].Should().Contain("Retry 1");
        retryLogs[1].Should().Contain("Retry 2");
    }

    // Real 503s, not exceptions, so HttpClientResiliencePredicates.IsTransient treats
    // them as retryable without any custom ShouldHandle wiring.
    private sealed class TransientFailureThenSuccessHandler(int failuresBeforeSuccess) : HttpMessageHandler
    {
        private int _callCount;

        public int CallCount => _callCount;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var attempt = Interlocked.Increment(ref _callCount);

            if (attempt <= failuresBeforeSuccess)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { RequestMessage = request });
            }

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new StringContent("""[{"q":"Test quote","a":"Test Author"}]""")
            };
            return Task.FromResult(response);
        }
    }

    private sealed class ListLoggerProvider : ILoggerProvider
    {
        public List<string> Messages { get; } = [];

        public ILogger CreateLogger(string categoryName) => new ListLogger(Messages);

        public void Dispose() { }

        private sealed class ListLogger(List<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                lock (messages)
                {
                    messages.Add(formatter(state, exception));
                }
            }
        }
    }
}
