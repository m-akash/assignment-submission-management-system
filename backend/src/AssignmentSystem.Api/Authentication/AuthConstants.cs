namespace AssignmentSystem.Api.Authentication;

/// <summary>
/// Auth-related constants shared across the API: the refresh-token cookie name and
/// a factory for its standard options (HttpOnly, Secure in prod, SameSite=Lax).
/// </summary>
internal static class AuthConstants
{
    public const string RefreshTokenCookie = "asm_refresh";

    /// <summary>
    /// The cookie rides on the root path so the Next.js proxy can see it on document
    /// requests and decide whether to gate the dashboard. Scoping it to
    /// <c>/api/v1/auth</c> would keep it off other API calls — but it would also stop
    /// the proxy from ever reading it, since document paths do not start with that
    /// prefix and the proxy gate would redirect signed-in users back to the login.
    /// The cookie is HttpOnly, so the only thing that can read it is server-side code.
    /// </summary>
    private const string RefreshCookiePath = "/";

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
