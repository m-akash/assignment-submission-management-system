namespace AssignmentSystem.Application.Abstractions;

/// <summary>
/// Issues and redeems the single-use links that let a new user choose their own password.
///
/// The plaintext token is returned exactly once, by <see cref="IssuePasswordSetupAsync"/>,
/// for the caller to put in an email. Nothing stores it — only its hash — so it cannot be
/// recovered afterwards, and a lost link means issuing a new one rather than looking the
/// old one up.
/// </summary>
public interface IPasswordSetupTokenService
{
    /// <summary>
    /// Issues a token for the user and <i>adds</i> the row without saving — the calling
    /// handler's <c>IUnitOfWork.SaveChangesAsync</c> commits it in the same transaction as
    /// the account it belongs to, alongside the notification that carries it. An account
    /// therefore never exists with a mail promising a link that was never persisted.
    /// </summary>
    Task<PasswordSetupIssue> IssuePasswordSetupAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Exchanges a plaintext token for a new password: verifies the token is unspent and
    /// unexpired, sets the hash on its user, marks the token consumed, and revokes every
    /// refresh token that user held. Saves its own work — this is the whole of the request.
    ///
    /// Returns false when the token is unknown, expired, already used, or belongs to an
    /// inactive account. Deliberately one undifferentiated answer: telling a caller which
    /// of those it was turns the endpoint into an oracle for guessing tokens.
    /// </summary>
    Task<bool> RedeemPasswordSetupAsync(string token, string newPassword, CancellationToken ct = default);

    /// <summary>
    /// Whether a token could be redeemed right now, without spending it — so the
    /// set-password page can say "this link has expired" before the user types a password
    /// rather than after. Carries the user's name for the page's greeting; possession of
    /// the token is what authorises knowing it.
    /// </summary>
    Task<PasswordSetupStatus> InspectPasswordSetupAsync(string token, CancellationToken ct = default);
}

/// <summary>The plaintext token to mail, and when it stops working.</summary>
public sealed record PasswordSetupIssue(string Token, DateTime ExpiresAtUtc);

/// <summary>
/// The answer to "is this link still good?". <see cref="FullName"/> is null whenever
/// <see cref="IsUsable"/> is false, so an unusable token reveals nothing about who it
/// belonged to.
/// </summary>
public sealed record PasswordSetupStatus(bool IsUsable, string? FullName, DateTime? ExpiresAtUtc);
