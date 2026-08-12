namespace Quotes.Tests.Unit;

internal sealed class FakeClock : IClock
{
    public DateTime UtcNow { get; set; }
}
