using AssignmentSystem.Domain.Common;

namespace AssignmentSystem.Domain.Users;

/// <summary>
/// A single-use, expiring capability to set one user's password without knowing the
/// current one. Issued when an admin creates the account and mailed to the user as a
/// link, which is why the account-created email can be useful without ever containing a
/// password: the link proves control of the mailbox, and the password itself is chosen by
/// the person who will use it and travels only over the HTTPS request that sets it.
///
/// Only the SHA-256 <see cref="TokenHash"/> is stored, exactly as for
/// <see cref="RefreshToken"/>. A leaked database dump therefore cannot be replayed
/// against this endpoint — the plaintext exists only in the email that was sent.
///
/// Consumption is recorded rather than the row being deleted, so "this link was already
/// used" is a distinguishable answer from "this link never existed", and a second click
/// on a link in an old email fails loudly instead of silently resetting a password.
/// </summary>
public sealed class PasswordSetupToken : BaseEntity
{
    public Guid UserId { get; private set; }
    public ApplicationUser User { get; private set; } = null!;

    /// <summary>SHA-256 hash of the plaintext token that went in the email.</summary>
    public string TokenHash { get; private set; } = null!;

    public DateTime ExpiresAtUtc { get; private set; }

    /// <summary>When the token was exchanged for a password. Null while still usable.</summary>
    public DateTime? ConsumedAtUtc { get; private set; }

    private PasswordSetupToken() { }

    public static PasswordSetupToken Issue(Guid userId, string tokenHash, DateTime expiresAtUtc, DateTime nowUtc)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("User id is required.");
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new DomainException("Token hash is required.");
        }

        if (expiresAtUtc <= nowUtc)
        {
            throw new DomainException("Password setup token expiry must be in the future.");
        }

        return new PasswordSetupToken
        {
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAtUtc = expiresAtUtc,
        };
    }

    /// <summary>
    /// Whether the token may still be exchanged. Takes the clock as an argument rather
    /// than reading <c>DateTime.UtcNow</c> so expiry is testable without waiting.
    /// </summary>
    public bool IsUsableAt(DateTime nowUtc) => ConsumedAtUtc is null && nowUtc < ExpiresAtUtc;

    /// <summary>
    /// Marks the token spent. Throws rather than returning false on a second call: the
    /// caller must check <see cref="IsUsableAt"/> first, and reaching here twice means a
    /// race got past that check, which is exactly the case that must not set a password.
    /// </summary>
    public void Consume(DateTime consumedAtUtc)
    {
        if (ConsumedAtUtc is not null)
        {
            throw new DomainException("This password setup link has already been used.");
        }

        if (consumedAtUtc >= ExpiresAtUtc)
        {
            throw new DomainException("This password setup link has expired.");
        }

        ConsumedAtUtc = consumedAtUtc;
    }
}
