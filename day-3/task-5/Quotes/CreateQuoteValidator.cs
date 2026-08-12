namespace Quotes;

public sealed class CreateQuoteValidator
{
    public const int MaxTextLength = 500;

    public ValidationResult Validate(string? text, string? ownerId)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(text))
        {
            errors.Add("Quote text is required.");
        }
        else if (text.Trim().Length > MaxTextLength)
        {
            errors.Add($"Quote text cannot be longer than {MaxTextLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(ownerId))
        {
            errors.Add("Owner id is required.");
        }

        return errors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failure(errors.ToArray());
    }
}
