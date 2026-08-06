using AssignmentSystem.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace AssignmentSystem.Infrastructure.Authentication;

/// <summary>
/// Adapts <see cref="AuthOptions"/> to the narrow <see cref="ILoginThrottleSettings"/> port
/// the Application layer sees, so <c>LoginHandler</c> needs no configuration plumbing.
/// </summary>
internal sealed class LoginThrottleSettings : ILoginThrottleSettings
{
    private readonly AuthOptions _options;

    public LoginThrottleSettings(IOptions<AuthOptions> options) => _options = options.Value;

    // Clamped: a misconfigured 0 would lock every account on its first typo.
    public int MaxFailedLoginAttempts => Math.Max(1, _options.MaxFailedLoginAttempts);

    public TimeSpan LockoutDuration => TimeSpan.FromMinutes(Math.Max(1, _options.LockoutMinutes));
}
