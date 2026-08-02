namespace AssignmentSystem.Api.Authentication;

/// <summary>
/// Auth-related constants shared across the API: the refresh-token cookie name and
/// a factory for its standard options (HttpOnly, Secure in prod, SameSite=Lax).
/// </summary>
internal static class AuthConstants
{
    public const string RefreshTokenCookie = "asm_refresh";

    /// <summary>Builds fresh cookie options (CookieOptions is mutable, so no shared instance).</summary>
    public static CookieOptions BuildRefreshCookieOptions(bool isHttps)
        => new()
        {
            HttpOnly = true,
            Secure = isHttps,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(7),
            Path = "/api/v1/auth",
        };
}
