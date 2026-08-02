using AssignmentSystem.Application.Common.Handlers;

namespace AssignmentSystem.Application.Features.Auth;

/// <summary>Login request body.</summary>
public sealed record LoginCommand(string Email, string Password, string? ClientIp = null) : ICommand<AuthResult>;

/// <summary>Refresh request — refresh token read from the httpOnly cookie.</summary>
public sealed record RefreshTokenCommand(string RefreshToken, string? ClientIp = null) : ICommand<AuthResult>;

/// <summary>Logout request — revokes the supplied refresh token.</summary>
public sealed record RevokeTokenCommand(string RefreshToken) : ICommand;

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
