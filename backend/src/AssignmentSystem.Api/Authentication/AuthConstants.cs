namespace AssignmentSystem.Api.Authentication;

/// <summary>
/// Auth-related constants shared across the API: the refresh-token cookie name and
/// a factory for its standard options (HttpOnly, Secure in prod, SameSite=Lax).
/// </summary>
internal static class AuthConstants
{
    public const string RefreshTokenCookie = "asm_refresh";

    /// <summary>
    /// Scopes the cookie to the auth endpoints, so it is not sent with every API call.
    /// Deleting the cookie requires the same path, hence the shared constant.
    /// </summary>
    private const string RefreshCookiePath = "/api/v1/auth";

    /// <summary>Builds fresh cookie options (CookieOptions is mutable, so no shared instance).</summary>
    public static CookieOptions BuildRefreshCookieOptions(bool isHttps)
        => Base(isHttps, expires: DateTimeOffset.UtcNow.AddDays(7));

    /// <summary>
    /// Options for removing the cookie. A browser only matches a deletion against a
    /// cookie with the same name *and* path, so these must mirror the write options.
    /// </summary>
    public static CookieOptions BuildRefreshCookieDeleteOptions(bool isHttps)
        => Base(isHttps, expires: null);

    private static CookieOptions Base(bool isHttps, DateTimeOffset? expires)
        => new()
        {
            HttpOnly = true,
            Secure = isHttps,
            SameSite = SameSiteMode.Lax,
            Expires = expires,
            Path = RefreshCookiePath,
        };
}
