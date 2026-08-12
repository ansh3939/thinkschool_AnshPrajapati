namespace Quotes;

public interface IClock
{
    DateTime UtcNow { get; }
}
