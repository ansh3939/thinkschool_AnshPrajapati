namespace Quotes;

public sealed record ValidationResult(bool IsValid, string[] Errors)
{
    public static ValidationResult Success() => new(true, []);

    public static ValidationResult Failure(params string[] errors) => new(false, errors);
}
