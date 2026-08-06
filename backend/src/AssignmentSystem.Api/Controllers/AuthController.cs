using AssignmentSystem.Api.Authentication;
using AssignmentSystem.Api.Common;
using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Application.Features.Auth;
using AssignmentSystem.Application.Features.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AssignmentSystem.Api.Controllers;

/// <summary>
/// Authentication endpoints. The refresh token travels in an httpOnly cookie (never
/// accessible to client JS); the access token is returned in the JSON body.
/// </summary>
[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public AuthController(IDispatcher dispatcher) => _dispatcher = dispatcher;

    [HttpPost("login")]
    [EnableRateLimiting(RateLimitPolicies.Credentials)]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseBody>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var command = new LoginCommand(request.Email, request.Password, GetClientIp());
        var result = await _dispatcher.SendAsync(command, ct);

        if (result.IsSuccess)
        {
            SetRefreshCookie(result.Value!.RefreshToken);
        }

        // Project out the refresh token — it only lives in the cookie, never the body.
        return result.ToActionResult(this, a => new AuthResponseBody(
            a.UserId, a.Email, a.FullName, a.Role, a.AccessToken, a.AccessTokenExpiresAtUtc));
    }

    [HttpPost("refresh")]
    [EnableRateLimiting(RateLimitPolicies.Credentials)]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseBody>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        var cookieToken = Request.Cookies[AuthConstants.RefreshTokenCookie];
        if (string.IsNullOrWhiteSpace(cookieToken))
        {
            return Error("Auth.InvalidRefreshToken", "The refresh token is invalid or expired.", StatusCodes.Status401Unauthorized);
        }

        var result = await _dispatcher.SendAsync(new RefreshTokenCommand(cookieToken, GetClientIp()), ct);

        if (result.IsSuccess)
        {
            SetRefreshCookie(result.Value!.RefreshToken);
        }

        return result.ToActionResult(this, a => new AuthResponseBody(
            a.UserId, a.Email, a.FullName, a.Role, a.AccessToken, a.AccessTokenExpiresAtUtc));
    }

    /// <summary>
    /// The caller's own profile, including class membership. The frontend calls this
    /// after login/refresh to rehydrate its session — the login body deliberately
    /// carries only what is needed to authenticate.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var result = await _dispatcher.QueryAsync(new GetCurrentUserQuery(), ct);
        return result.ToActionResult(this);
    }

    /// <summary>
    /// Revokes the refresh token and clears its cookie. Deliberately anonymous: the
    /// access token lives five minutes, so requiring one would leave a client unable
    /// to log out of an idle session. Possession of the cookie is the authorisation.
    /// </summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var cookieToken = Request.Cookies[AuthConstants.RefreshTokenCookie];
        if (!string.IsNullOrWhiteSpace(cookieToken))
        {
            await _dispatcher.SendAsync(new RevokeTokenCommand(cookieToken), ct);
        }

        Response.Cookies.Delete(
            AuthConstants.RefreshTokenCookie,
            AuthConstants.BuildRefreshCookieDeleteOptions(Request.IsHttps));

        return NoContent();
    }

    /// <summary>
    /// Whether a password-setup link is still usable, without spending it. Exists so the
    /// set-password page can say "this link has expired" before asking for a password rather
    /// than after — the alternative is a user typing a password twice into a dead form.
    ///
    /// The token travels in the query string because that is where the emailed link puts it.
    /// That does mean it can end up in an access log, which is why it is single-use and
    /// short-lived, and why the password itself only ever goes in the POST body below.
    /// </summary>
    [HttpGet("set-password")]
    [EnableRateLimiting(RateLimitPolicies.Credentials)]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<PasswordSetupStatusDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPasswordSetupStatus([FromQuery] string token, CancellationToken ct)
    {
        var result = await _dispatcher.QueryAsync(new GetPasswordSetupStatusQuery(token), ct);
        return result.ToActionResult(this);
    }

    /// <summary>
    /// Sets a password from a single-use setup link, and drops every session the account had.
    ///
    /// Anonymous by necessity — the caller has no password yet, which is the whole point —
    /// with possession of the token as the authorisation. Returns 204 rather than signing the
    /// user in: a fresh login through the normal path is one less way for this endpoint to
    /// hand out credentials.
    /// </summary>
    [HttpPost("set-password")]
    [EnableRateLimiting(RateLimitPolicies.Credentials)]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SetPassword([FromBody] SetPasswordRequest request, CancellationToken ct)
    {
        var result = await _dispatcher.SendAsync(
            new SetPasswordCommand(request.Token, request.NewPassword), ct);

        return result.IsSuccess ? NoContent() : result.ToActionResult(this);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private void SetRefreshCookie(string token)
    {
        Response.Cookies.Append(
            AuthConstants.RefreshTokenCookie,
            token,
            AuthConstants.BuildRefreshCookieOptions(Request.IsHttps));
    }

    private string? GetClientIp() =>
        HttpContext.Connection.RemoteIpAddress?.ToString();

    private ObjectResult Error(string code, string message, int statusCode)
    {
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = "Authentication failed.",
            Detail = message,
            Type = $"https://httpstatuses.io/{statusCode}",
            Instance = Request.Path,
        };
        problem.Extensions["code"] = code;
        return new ObjectResult(problem)
        {
            StatusCode = statusCode,
            ContentTypes = { "application/problem+json" },
        };
    }
}

public sealed record LoginRequest(string Email, string Password);

/// <summary>
/// Set-password body. The token is posted rather than left in the URL for this call, so the
/// password and the capability that authorises it share one request and neither is logged.
/// </summary>
public sealed record SetPasswordRequest(string Token, string NewPassword);

/// <summary>Login/refresh response body. The refresh token is NEVER included here — it lives only in the httpOnly cookie.</summary>
public sealed record AuthResponseBody(
    Guid UserId,
    string Email,
    string FullName,
    Domain.Enums.Role Role,
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc);
