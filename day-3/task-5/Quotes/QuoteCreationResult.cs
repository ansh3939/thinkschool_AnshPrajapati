namespace Quotes;

public sealed record QuoteCreationResult(bool IsSuccess, Quote? Quote, string[] Errors)
{
    public static QuoteCreationResult Success(Quote quote) => new(true, quote, []);

    public static QuoteCreationResult Failure(params string[] errors) => new(false, null, errors);
}
