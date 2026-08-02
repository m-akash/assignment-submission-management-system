using AssignmentSystem.Domain.Common;

namespace AssignmentSystem.Domain.Users;

/// <summary>
/// A refresh token for JWT rotation. Only the SHA-256 <see cref="TokenHash"/> is
/// stored — never the plaintext. Single-use rotation with reuse detection: when a
/// token that is already revoked is presented again, the entire family is revoked
/// (rule X8).
/// </summary>
public sealed class RefreshToken : BaseEntity
{
    public Guid UserId { get; private set; }
    public ApplicationUser User { get; private set; } = null!;

    /// <summary>SHA-256 hash of the plaintext token.</summary>
    public string TokenHash { get; private set; } = null!;

    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public string? ReplacedByTokenHash { get; private set; }
    public string? CreatedByIp { get; private set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAtUtc;
    public bool IsRevoked => RevokedAtUtc is not null;
    public bool IsActive => !IsRevoked && !IsExpired;

    private RefreshToken() { }

    public static RefreshToken Create(Guid userId, string tokenHash, DateTime expiresAtUtc, string? createdByIp)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("User id is required.");
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new DomainException("Token hash is required.");
        }

        if (expiresAtUtc <= DateTime.UtcNow)
        {
            throw new DomainException("Refresh token expiry must be in the future.");
        }

        return new RefreshToken
        {
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAtUtc = expiresAtUtc,
            CreatedByIp = createdByIp,
        };
    }

    public void Revoke(string? replacedByTokenHash = null)
    {
        RevokedAtUtc = DateTime.UtcNow;
        ReplacedByTokenHash = replacedByTokenHash;
    }
}
