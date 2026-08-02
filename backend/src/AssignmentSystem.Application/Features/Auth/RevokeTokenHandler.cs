using AssignmentSystem.Application.Abstractions;
using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Shared.Common;
using Serilog;

namespace AssignmentSystem.Application.Features.Auth;

/// <summary>
/// Revokes a single refresh token (logout from one device). Idempotent: an
/// already-revoked or unknown token still returns success to avoid information leakage.
/// </summary>
public sealed class RevokeTokenHandler : ICommandHandler<RevokeTokenCommand>
{
    private readonly IJwtTokenService _jwtTokenService;

    public RevokeTokenHandler(IJwtTokenService jwtTokenService) => _jwtTokenService = jwtTokenService;

    public async Task<Result> HandleAsync(RevokeTokenCommand command, CancellationToken ct = default)
    {
        // Rotate with a throwaway lookup to locate + revoke; we don't need the new token.
        // The token service revokes the old token during rotation. For a pure revoke
        // (without issuing a new token) we attempt rotation and discard the result —
        // invalid tokens simply return success.
        await _jwtTokenService.RotateRefreshTokenAsync(command.RefreshToken, null, ct);
        Log.Information("Refresh token revoked on logout.");
        return Result.Success();
    }
}
