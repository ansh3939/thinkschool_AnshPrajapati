using QuotesApi.Clock;

namespace Quotes.Tests.Integration;

/// <summary>Deterministic clock used instead of <see cref="SystemClock"/> in tests.</summary>
public sealed class FakeClock(DateTime utcNow) : IClock
{
    public DateTime UtcNow { get; set; } = utcNow;
}
