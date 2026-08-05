using System.Security.Cryptography;
using System.Text;
using AssignmentSystem.Application.Abstractions;
using AssignmentSystem.Domain.Common;
using AssignmentSystem.Domain.Users;
using AssignmentSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AssignmentSystem.Infrastructure.Authentication;

/// <summary>
/// Issues and redeems password-setup links. Mirrors <see cref="JwtTokenService"/>'s token
/// handling deliberately — 32 cryptographically random bytes, SHA-256 stored, plaintext
/// returned once — so there is one way tokens work in this codebase rather than two.
///
/// The one difference is the encoding: this token travels in a URL, so it is base64url
/// rather than plain base64. A '+' in a query string is a space, and a link that silently
/// stops working when a mail client re-encodes it is worse than no link at all.
/// </summary>
internal sealed class PasswordSetupTokenService : IPasswordSetupTokenService
{
    private readonly AppDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IClock _clock;
    private readonly AuthOptions _options;
    private readonly ILogger<PasswordSetupTokenService> _logger;

    public PasswordSetupTokenService(
        AppDbContext context,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        IClock clock,
        IOptions<AuthOptions> options,
        ILogger<PasswordSetupTokenService> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<PasswordSetupIssue> IssuePasswordSetupAsync(Guid userId, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        // Clamped: a misconfigured 0 or negative would make every link dead on arrival,
        // and the domain would throw on expiry-in-the-past rather than explain why.
        var hours = Math.Max(1, _options.PasswordSetupTokenHours);
        var expiresAt = now.AddHours(hours);

        var (plain, hash) = NewToken();

        // Add-only: the caller's UnitOfWork commits this with the user it belongs to.
        await _context.PasswordSetupTokens.AddAsync(
            PasswordSetupToken.Issue(userId, hash, expiresAt, now), ct);

        return new PasswordSetupIssue(plain, expiresAt);
    }

    public async Task<bool> RedeemPasswordSetupAsync(string token, string newPassword, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var hash = HashToken(token);
        var now = _clock.UtcNow;

        // The user is included rather than fetched separately: the row is worthless without
        // them, and the query filter on Users would otherwise hide a soft-deleted owner in a
        // way that looks like a missing token.
        var setupToken = await _context.PasswordSetupTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (setupToken is null || !setupToken.IsUsableAt(now))
        {
            _logger.LogWarning(
                "Rejected password setup: token {State}.",
                setupToken is null ? "not found" : setupToken.ConsumedAtUtc is not null ? "already used" : "expired");
            return false;
        }

        if (setupToken.User is null || !setupToken.User.IsActive)
        {
            _logger.LogWarning(
                "Rejected password setup for user {UserId}: account is missing or deactivated.",
                setupToken.UserId);
            return false;
        }

        setupToken.User.SetPasswordHash(_passwordHasher.Hash(newPassword));
        setupToken.Consume(now);

        await _context.SaveChangesAsync(ct);

        // After the password is in place, not before: if this threw, the user would be left
        // signed out of every device with their old password still in force.
        //
        // Sessions are dropped because setting a password is the point at which an account
        // changes hands — from the admin who typed the initial one to the person using it.
        // Anything already signed in with the old password should not survive that.
        await _jwtTokenService.RevokeAllForUserAsync(setupToken.UserId, ct);

        _logger.LogInformation("User {UserId} set their password via a setup link.", setupToken.UserId);
        return true;
    }

    public async Task<PasswordSetupStatus> InspectPasswordSetupAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new PasswordSetupStatus(false, null, null);
        }

        var hash = HashToken(token);

        var setupToken = await _context.PasswordSetupTokens
            .AsNoTracking()
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (setupToken is null
            || !setupToken.IsUsableAt(_clock.UtcNow)
            || setupToken.User is null
            || !setupToken.User.IsActive)
        {
            // No name and no expiry on the unusable path — an unusable token must not become
            // a way to look up whose account it was.
            return new PasswordSetupStatus(false, null, null);
        }

        return new PasswordSetupStatus(true, setupToken.User.FullName, setupToken.ExpiresAtUtc);
    }

    /// <summary>
    /// 32 random bytes, base64url-encoded — URL-safe with no padding, so the token survives
    /// being pasted, redirected, and re-encoded on its way from an inbox to the API.
    /// </summary>
    private static (string Plain, string Hash) NewToken()
    {
        var plain = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        return (plain, HashToken(plain));
    }

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
