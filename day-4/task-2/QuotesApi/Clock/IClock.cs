namespace QuotesApi.Clock;

public interface IClock
{
    DateTime UtcNow { get; }
}
