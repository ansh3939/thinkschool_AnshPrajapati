namespace QuotesApi.Models;

public class Quote
{
    private Quote()
    {
    }

    private Quote(string author, string text)
    {
        Author = author;
        Text = text;
    }

    public int Id { get; private set; }

    public string Author { get; private set; } = string.Empty;

    public string Text { get; private set; } = string.Empty;

    public bool IsDeleted { get; private set; }

    public static QuoteCreationResult Create(string? author, string? text)
    {
        var normalizedAuthor = author?.Trim();
        if (string.IsNullOrEmpty(normalizedAuthor))
            return QuoteCreationResult.Failure("author", "Author is required.");

        if (normalizedAuthor.Length > 200)
            return QuoteCreationResult.Failure("author", "Author must be 200 characters or fewer.");

        var normalizedText = text?.Trim();
        if (string.IsNullOrEmpty(normalizedText))
            return QuoteCreationResult.Failure("text", "Text is required.");

        if (normalizedText.Length > 1000)
            return QuoteCreationResult.Failure("text", "Text must be 1000 characters or fewer.");

        return QuoteCreationResult.Success(new Quote(normalizedAuthor, normalizedText));
    }

    public void Delete()
    {
        IsDeleted = true;
    }
}

public sealed record QuoteCreationResult(
    Quote? Quote,
    string? ErrorField,
    string? Error)
{
    public bool IsSuccess => Quote is not null;

    public static QuoteCreationResult Success(Quote quote) =>
        new(quote, null, null);

    public static QuoteCreationResult Failure(string errorField, string error) =>
        new(null, errorField, error);
}
