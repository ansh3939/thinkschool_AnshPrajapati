namespace DependencyInjectionApi.Clock;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}