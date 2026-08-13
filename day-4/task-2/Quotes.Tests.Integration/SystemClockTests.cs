using FluentAssertions;
using QuotesApi.Clock;

namespace Quotes.Tests.Integration;

/// <summary>
/// The app wires this up as the real IClock in Program.cs, but every integration test
/// replaces it with FakeClock, so it never actually runs under test.
/// </summary>
public sealed class SystemClockTests
{
    [Fact]
    public void UtcNow_ReturnsCurrentUtcTime()
    {
        // Arrange
        var clock = new SystemClock();

        // Act
        var before = DateTime.UtcNow;
        var result = clock.UtcNow;
        var after = DateTime.UtcNow;

        // Assert
        result.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        result.Kind.Should().Be(DateTimeKind.Utc);
    }
}
