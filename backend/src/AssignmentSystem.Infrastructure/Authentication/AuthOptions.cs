namespace AssignmentSystem.Infrastructure.Authentication;

/// <summary>
/// Account-lifecycle settings bound from the "Auth" section. Kept out of
/// <see cref="JwtOptions"/> on purpose: nothing here is a JWT, and a password-setup link
/// that outlives an access token by four orders of magnitude does not belong in the same
/// group of knobs.
/// </summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>
    /// How long a password-setup link stays valid. Two days covers an account created on a
    /// Friday afternoon without leaving a way into the account open for a fortnight.
    /// </summary>
    public int PasswordSetupTokenHours { get; set; } = 48;

    /// <summary>
    /// Minimum length enforced when a user sets their own password. Matches the admin-side
    /// rule in <c>CreateUserRequestValidator</c> — a user choosing their own password should
    /// not face a weaker bar than the one an admin faces on their behalf.
    /// </summary>
    public int MinimumPasswordLength { get; set; } = 6;

    /// <summary>
    /// Consecutive wrong passwords before an account is locked. Five is enough room for a
    /// person who genuinely mistypes and forgets which of two passwords they used, and far
    /// too few for anyone working through a list.
    /// </summary>
    public int MaxFailedLoginAttempts { get; set; } = 5;

    /// <summary>
    /// How long the lock holds. Fifteen minutes is short enough that a locked-out teacher is
    /// inconvenienced rather than blocked, and long enough that it caps an attacker at twenty
    /// guesses an hour per account — which no wordlist survives.
    /// </summary>
    public int LockoutMinutes { get; set; } = 15;
}
