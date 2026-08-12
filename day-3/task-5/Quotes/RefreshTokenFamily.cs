namespace Quotes;

public sealed class RefreshTokenFamily
{
    public const int LifetimeDays = 7;

    private readonly List<StoredRefreshToken> tokens = [];
    private readonly IClock clock;

    private RefreshTokenFamily(string userId, IClock clock)
    {
        UserId = userId;
        FamilyId = Guid.NewGuid().ToString();
        this.clock = clock;
    }

    public string UserId { get; }

    public string FamilyId { get; }

    public IReadOnlyCollection<StoredRefreshToken> Tokens => tokens;

    public StoredRefreshToken ActiveToken => tokens.Single(token => token.RevokedAt is null);

    public static RefreshTokenFamily Create(string userId, string firstToken, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        var family = new RefreshTokenFamily(userId, clock);
        family.tokens.Add(new StoredRefreshToken(firstToken, userId, family.FamilyId, clock.UtcNow.AddDays(LifetimeDays)));
        return family;
    }

    public RefreshResult Rotate(string? presentedToken, string nextToken)
    {
        if (string.IsNullOrWhiteSpace(presentedToken))
        {
            return new RefreshResult(RefreshResultKind.MissingToken, null);
        }

        var currentToken = tokens.SingleOrDefault(token => token.Token == presentedToken);
        if (currentToken is null)
        {
            return new RefreshResult(RefreshResultKind.UnknownToken, null);
        }

        if (currentToken.ExpiresAt <= clock.UtcNow)
        {
            return new RefreshResult(RefreshResultKind.ExpiredToken, null);
        }

        if (currentToken.RevokedAt is not null)
        {
            if (!string.IsNullOrWhiteSpace(currentToken.ReplacedByToken))
            {
                RevokeActiveTokens();
                return new RefreshResult(RefreshResultKind.ReuseDetected, null);
            }

            return new RefreshResult(RefreshResultKind.RevokedToken, null);
        }

        currentToken.RevokedAt = clock.UtcNow;
        currentToken.ReplacedByToken = nextToken;
        tokens.Add(new StoredRefreshToken(nextToken, UserId, FamilyId, clock.UtcNow.AddDays(LifetimeDays)));

        return new RefreshResult(RefreshResultKind.Rotated, nextToken);
    }

    private void RevokeActiveTokens()
    {
        foreach (var token in tokens.Where(token => token.RevokedAt is null))
        {
            token.RevokedAt = clock.UtcNow;
        }
    }
}
