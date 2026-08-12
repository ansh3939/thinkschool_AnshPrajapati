namespace QuotesApi.Authentication;

public sealed class EntraOptions
{
    public string? TenantId { get; init; }
    public string? ClientId { get; init; }
    public string? Authority { get; init; }
    public string? Audience { get; init; }
}
