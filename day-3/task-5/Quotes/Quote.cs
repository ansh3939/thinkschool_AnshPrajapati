namespace Quotes;

public sealed class Quote
{
    private Quote(string text, string ownerId, DateTime createdAt)
    {
        Text = text;
        OwnerId = ownerId;
        CreatedAt = createdAt;
    }

    public string Text { get; }

    public string OwnerId { get; }

    public DateTime CreatedAt { get; }

    public static QuoteCreationResult Create(string? text, string? ownerId, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        var validation = new CreateQuoteValidator().Validate(text, ownerId);
        if (!validation.IsValid)
        {
            return QuoteCreationResult.Failure(validation.Errors);
        }

        return QuoteCreationResult.Success(new Quote(text!.Trim(), ownerId!.Trim(), clock.UtcNow));
    }
}
