using AssignmentSystem.Application.Common.Handlers;

namespace AssignmentSystem.Application.Features.Auth;

/// <summary>Login request body.</summary>
public sealed record LoginCommand(string Email, string Password, string? ClientIp = null) : ICommand<AuthResult>;

/// <summary>Refresh request — refresh token read from the httpOnly cookie.</summary>
public sealed record RefreshTokenCommand(string RefreshToken, string? ClientIp = null) : ICommand<AuthResult>;

/// <summary>Logout request — revokes the supplied refresh token.</summary>
public sealed record RevokeTokenCommand(string RefreshToken) : ICommand;

/// <summary>
/// Exchanges a password-setup token for a chosen password. Anonymous by necessity — the
/// whole point is that the caller cannot sign in yet — with possession of the single-use
/// token as the authorisation.
/// </summary>
public sealed record SetPasswordCommand(string Token, string NewPassword) : ICommand;

/// <summary>Checks a setup token without spending it, so the page can fail early.</summary>
public sealed record GetPasswordSetupStatusQuery(string Token) : IQuery<PasswordSetupStatusDto>;

/// <summary>
/// Whether a setup link is still good. <see cref="FullName"/> is present only when it is,
/// so an expired or unknown token discloses nothing about the account behind it.
/// </summary>
public sealed record PasswordSetupStatusDto(bool IsUsable, string? FullName, DateTime? ExpiresAtUtc);

/// <summary>Authentication response returned to the client (refresh token goes in the cookie).</summary>
public sealed record AuthResult(
    Guid UserId,
    string Email,
    string FullName,
    Domain.Enums.Role Role,
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc);
