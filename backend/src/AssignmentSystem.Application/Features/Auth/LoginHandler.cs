using AssignmentSystem.Application.Abstractions;
using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.Common;
using AssignmentSystem.Domain.Users;
using AssignmentSystem.Shared.Common;
using Microsoft.Extensions.Logging;

namespace AssignmentSystem.Application.Features.Auth;

/// <summary>
/// Authenticates a user by email + password and issues an access token + rotating
/// refresh token. Never reveals whether the email exists vs the password is wrong
/// (same generic error) to avoid user enumeration.
///
/// Failed attempts are counted on the account and lock it for a cooling-off window once the
/// threshold is reached. This sits behind the endpoint's per-IP rate limit and does a
/// different job: the rate limiter stops one caller hammering the API, this caps total
/// guesses against a single account no matter how many addresses they come from.
/// </summary>
public sealed class LoginHandler : ICommandHandler<LoginCommand, AuthResult>
{
    /// <summary>
    /// The one error every failure path returns. A lockout deliberately looks identical to a
    /// wrong password: telling an attacker "this account is now locked" confirms the address
    /// exists and tells them exactly when to resume.
    /// </summary>
    private static Error InvalidCredentials =>
        Error.Unauthorized("Auth.InvalidCredentials", "Invalid email or password.");

    private readonly IRepository<ApplicationUser> _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ILoginThrottleSettings _throttle;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ILogger<LoginHandler> _logger;

    public LoginHandler(
        IRepository<ApplicationUser> userRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        ILoginThrottleSettings throttle,
        IUnitOfWork unitOfWork,
        IClock clock,
        ILogger<LoginHandler> logger)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _throttle = throttle;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<AuthResult>> HandleAsync(LoginCommand command, CancellationToken ct = default)
    {
        // Tracked, unlike the read-only lookups elsewhere: a failed attempt is a write.
        var user = await _userRepository.FirstOrDefaultAsync(new UserByEmailSpecification(command.Email), ct);

        if (user is null || !user.IsActive)
        {
            _logger.LogWarning("Failed login for unknown or inactive email {Email}.", command.Email);
            return Result<AuthResult>.Failure(InvalidCredentials);
        }

        var now = _clock.UtcNow;

        if (user.IsLockedOut(now))
        {
            // Refused without checking the password at all, so a locked account cannot be used
            // as an oracle for whether a guess was right.
            _logger.LogWarning(
                "Login refused for {UserId}: locked out until {LockoutEndUtc}.", user.Id, user.LockoutEndUtc);
            return Result<AuthResult>.Failure(InvalidCredentials);
        }

        if (!_passwordHasher.Verify(user.PasswordHash, command.Password))
        {
            user.RegisterFailedLogin(now, _throttle.MaxFailedLoginAttempts, _throttle.LockoutDuration);
            _userRepository.Update(user);
            await _unitOfWork.SaveChangesAsync(ct);

            _logger.LogWarning(
                "Failed login for {UserId}: attempt {Attempt} of {Max}{Locked}.",
                user.Id,
                user.FailedLoginAttempts,
                _throttle.MaxFailedLoginAttempts,
                user.IsLockedOut(now) ? " — account now locked" : string.Empty);

            return Result<AuthResult>.Failure(InvalidCredentials);
        }

        var (accessToken, accessExpires) = _jwtTokenService.GenerateAccessToken(
            user.Id, user.EmailValue, user.FullName, user.Role);
        var (refreshToken, refreshExpires) = await _jwtTokenService.GenerateRefreshTokenAsync(user.Id, command.ClientIp, ct);

        // Clears the counter so a user who mistypes twice and then succeeds starts clean.
        user.RegisterSuccessfulLogin();
        _userRepository.Update(user);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("User {UserId} ({Role}) logged in.", user.Id, user.Role);

        return new AuthResult(
            user.Id, user.EmailValue, user.FullName, user.Role,
            accessToken, accessExpires, refreshToken, refreshExpires);
    }
}

/// <summary>
/// Finds a user by normalized email. Tracked on purpose — the login path writes the failed
/// attempt counter back, so a no-tracking read would silently drop it.
/// </summary>
internal sealed class UserByEmailSpecification : Specification<ApplicationUser>
{
    public UserByEmailSpecification(string email)
    {
        // Normalize once on the client side; EF translates the equality to SQL.
        var normalized = email.Trim().ToLowerInvariant();
        Criteria = u => u.Email.Value == normalized;
    }
}
