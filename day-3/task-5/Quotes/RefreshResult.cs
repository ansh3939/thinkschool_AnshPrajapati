namespace Quotes;

public enum RefreshResultKind
{
    Rotated,
    MissingToken,
    UnknownToken,
    ExpiredToken,
    RevokedToken,
    ReuseDetected
}

public sealed record RefreshResult(RefreshResultKind Kind, string? NewToken)
{
    public bool Succeeded => Kind == RefreshResultKind.Rotated;
}
