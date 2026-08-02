namespace AssignmentSystem.Infrastructure.Authentication;

/// <summary>
/// JWT configuration bound from the "Jwt" section. Key is the signing secret
/// (supplied via env var in production, never committed).
/// </summary>
internal sealed class JwtOptions
{
    public string Issuer { get; init; } = "AssignmentSystem.Api";
    public string Audience { get; init; } = "AssignmentSystem.Web";
    public int AccessTokenMinutes { get; init; } = 5;
    public int RefreshTokenDays { get; init; } = 7;
    public string Key { get; init; } = string.Empty;
}
