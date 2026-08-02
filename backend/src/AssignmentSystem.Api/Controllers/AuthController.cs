using AssignmentSystem.Api.Authentication;
using AssignmentSystem.Api.Common;
using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Application.Features.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssignmentSystem.Api.Controllers;

/// <summary>
/// Authentication endpoints. The refresh token travels in an httpOnly cookie (never
/// accessible to client JS); the access token is returned in the JSON body.
/// </summary>
[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly ICommandHandler<LoginCommand, AuthResult> _loginHandler;
    private readonly ICommandHandler<RefreshTokenCommand, AuthResult> _refreshHandler;
    private readonly ICommandHandler<RevokeTokenCommand> _revokeHandler;

    public AuthController(
        ICommandHandler<LoginCommand, AuthResult> loginHandler,
        ICommandHandler<RefreshTokenCommand, AuthResult> refreshHandler,
        ICommandHandler<RevokeTokenCommand> revokeHandler)
    {
        _loginHandler = loginHandler;
        _refreshHandler = refreshHandler;
        _revokeHandler = revokeHandler;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseBody>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var command = new LoginCommand(request.Email, request.Password, GetClientIp());
        var result = await _loginHandler.HandleAsync(command, ct);

        if (result.IsSuccess)
        {
            SetRefreshCookie(result.Value!.RefreshToken);
        }

        // Project out the refresh token — it only lives in the cookie, never the body.
        return result.ToActionResult(this, a => new AuthResponseBody(
            a.UserId, a.Email, a.FullName, a.Role, a.AccessToken, a.AccessTokenExpiresAtUtc));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseBody>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        var cookieToken = Request.Cookies[AuthConstants.RefreshTokenCookie];
        if (string.IsNullOrWhiteSpace(cookieToken))
        {
            return Error("Auth.InvalidRefreshToken", "The refresh token is invalid or expired.", StatusCodes.Status401Unauthorized);
        }

        var result = await _refreshHandler.HandleAsync(new RefreshTokenCommand(cookieToken, GetClientIp()), ct);

        if (result.IsSuccess)
        {
            SetRefreshCookie(result.Value!.RefreshToken);
        }

        return result.ToActionResult(this, a => new AuthResponseBody(
            a.UserId, a.Email, a.FullName, a.Role, a.AccessToken, a.AccessTokenExpiresAtUtc));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var cookieToken = Request.Cookies[AuthConstants.RefreshTokenCookie];
        if (!string.IsNullOrWhiteSpace(cookieToken))
        {
            await _revokeHandler.HandleAsync(new RevokeTokenCommand(cookieToken), ct);
        }

        Response.Cookies.Delete(AuthConstants.RefreshTokenCookie);
        return NoContent();
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

/// <summary>Login/refresh response body. The refresh token is NEVER included here — it lives only in the httpOnly cookie.</summary>
public sealed record AuthResponseBody(
    Guid UserId,
    string Email,
    string FullName,
    Domain.Enums.Role Role,
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc);
