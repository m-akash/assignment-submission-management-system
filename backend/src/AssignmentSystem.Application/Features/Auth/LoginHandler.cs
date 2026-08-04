using AssignmentSystem.Application.Abstractions;
using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Domain.Users;
using AssignmentSystem.Shared.Common;
using Microsoft.Extensions.Logging;
using Serilog;

namespace AssignmentSystem.Application.Features.Auth;

/// <summary>
/// Authenticates a user by email + password and issues an access token + rotating
/// refresh token. Never reveals whether the email exists vs the password is wrong
/// (same generic error) to avoid user enumeration.
/// </summary>
public sealed class LoginHandler : ICommandHandler<LoginCommand, AuthResult>
{
    private readonly IRepository<ApplicationUser> _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ILogger<LoginHandler> _logger;

    public LoginHandler(
        IRepository<ApplicationUser> userRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        ILogger<LoginHandler> logger)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _logger = logger;
    }

    public async Task<Result<AuthResult>> HandleAsync(LoginCommand command, CancellationToken ct = default)
    {
        // Use a spec to find by email — query filter hides soft-deleted users automatically.
        var userSpec = new UserByEmailSpecification(command.Email);
        var user = await _userRepository.FirstOrDefaultAsync(userSpec, ct);

        // Constant-ish failure path: validate the hash only if the user exists, then
        // return the SAME error either way to prevent user enumeration.
        if (user is null || !user.IsActive)
        {
            _logger.LogWarning("Failed login for unknown/inactive email {Email}", command.Email);
            return Result<AuthResult>.Failure(Error.Unauthorized("Auth.InvalidCredentials", "Invalid email or password."));
        }

        if (!_passwordHasher.Verify(user.PasswordHash, command.Password))
        {
            Log.Warning("Failed login: bad password for {UserId}", user.Id);
            return Result<AuthResult>.Failure(Error.Unauthorized("Auth.InvalidCredentials", "Invalid email or password."));
        }

        var (accessToken, accessExpires) = _jwtTokenService.GenerateAccessToken(
            user.Id, user.EmailValue, user.FullName, user.Role);
        var (refreshToken, refreshExpires) = await _jwtTokenService.GenerateRefreshTokenAsync(user.Id, command.ClientIp, ct);

        Log.Information("User {UserId} ({Role}) logged in", user.Id, user.Role);

        return new AuthResult(
            user.Id, user.EmailValue, user.FullName, user.Role,
            accessToken, accessExpires, refreshToken, refreshExpires);
    }
}

/// <summary>Finds a user by normalized email.</summary>
internal sealed class UserByEmailSpecification : Specification<ApplicationUser>
{
    public UserByEmailSpecification(string email)
    {
        // Normalize once on the client side; EF translates the equality to SQL.
        var normalized = email.Trim().ToLowerInvariant();
        Criteria = u => u.Email.Value == normalized;
        ApplyNoTracking();
    }
}
