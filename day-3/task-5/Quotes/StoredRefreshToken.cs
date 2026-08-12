namespace Quotes;

public sealed class StoredRefreshToken
{
    public StoredRefreshToken(string token, string userId, string familyId, DateTime expiresAt)
    {
        Token = token;
        UserId = userId;
        FamilyId = familyId;
        ExpiresAt = expiresAt;
    }

    public string Token { get; }

    public string UserId { get; }

    public string FamilyId { get; }

    public DateTime ExpiresAt { get; }

    public DateTime? RevokedAt { get; set; }

    public string? ReplacedByToken { get; set; }
}
