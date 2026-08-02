using AssignmentSystem.Application.Abstractions;
using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.Users;
using AssignmentSystem.Shared.Common;
using Serilog;

namespace AssignmentSystem.Application.Features.Auth;

/// <summary>
/// Rotates a refresh token into a new access + refresh pair. Delegates reuse detection
/// (revoke family on reuse) to <see cref="IJwtTokenService.RotateRefreshTokenAsync"/>,
/// then reloads the user to populate the full response envelope.
/// </summary>
public sealed class RefreshTokenHandler : ICommandHandler<RefreshTokenCommand, AuthResult>
{
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IRepository<ApplicationUser> _userRepository;

    public RefreshTokenHandler(IJwtTokenService jwtTokenService, IRepository<ApplicationUser> userRepository)
    {
        _jwtTokenService = jwtTokenService;
        _userRepository = userRepository;
    }

    public async Task<Result<AuthResult>> HandleAsync(RefreshTokenCommand command, CancellationToken ct = default)
    {
        var rotation = await _jwtTokenService.RotateRefreshTokenAsync(command.RefreshToken, command.ClientIp, ct);
        if (rotation is null)
        {
            Log.Warning("Rejected refresh-token rotation attempt (invalid/expired/reused).");
            return Result<AuthResult>.Failure(Error.Unauthorized("Auth.InvalidRefreshToken", "The refresh token is invalid or expired."));
        }

        var user = await _userRepository.GetByIdAsync(rotation.UserId, ct);
        if (user is null || !user.IsActive)
        {
            return Result<AuthResult>.Failure(Error.Unauthorized("Auth.InvalidRefreshToken", "The refresh token is invalid or expired."));
        }

        return new AuthResult(
            user.Id, user.EmailValue, user.FullName, user.Role,
            rotation.AccessToken, rotation.AccessExpiresAtUtc,
            rotation.NewRefreshToken, rotation.RefreshExpiresAtUtc);
    }
}
