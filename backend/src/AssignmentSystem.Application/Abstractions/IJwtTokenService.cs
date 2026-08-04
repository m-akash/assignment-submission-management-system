using AssignmentSystem.Domain.Enums;

namespace AssignmentSystem.Application.Abstractions;

/// <summary>
/// Issues and validates JWT access tokens and manages rotating refresh tokens.
/// Implementation lives in Infrastructure (token signing + DB-backed refresh store).
/// </summary>
public interface IJwtTokenService
{
    /// <summary>Issues a short-lived access token for the given user.</summary>
    (string AccessToken, DateTime ExpiresAtUtc) GenerateAccessToken(Guid userId, string email, string fullName, Role role);

    /// <summary>Issues a refresh token, persists its hash, returns the plaintext once.</summary>
    Task<(string RefreshToken, DateTime ExpiresAtUtc)> GenerateRefreshTokenAsync(Guid userId, string? createdByIp, CancellationToken ct = default);

    /// <summary>
    /// Validates a refresh token, rotates it (revokes old, issues new). Detects reuse
    /// of an already-revoked token and revokes the whole family (rule X8).
    /// Returns null if the token is invalid/expired/stolen.
    /// </summary>
    Task<RefreshTokenRotation?> RotateRefreshTokenAsync(string refreshToken, string? createdByIp, CancellationToken ct = default);

    /// <summary>Revokes all active refresh tokens for a user (logout-everywhere).</summary>
    Task RevokeAllForUserAsync(Guid userId, CancellationToken ct = default);
}

public sealed record RefreshTokenRotation(
    Guid UserId,
    string NewRefreshToken,
    DateTime RefreshExpiresAtUtc,
    string AccessToken,
    DateTime AccessExpiresAtUtc);
