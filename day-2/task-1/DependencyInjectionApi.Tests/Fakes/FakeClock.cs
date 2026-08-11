using DependencyInjectionApi.Clock;

namespace DependencyInjectionApi.Tests.Fakes;

public class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; set; }
}