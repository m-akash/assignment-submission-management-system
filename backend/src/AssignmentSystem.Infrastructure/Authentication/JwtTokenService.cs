using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AssignmentSystem.Application.Abstractions;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Domain.Users;
using AssignmentSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AssignmentSystem.Infrastructure.Authentication;

/// <summary>
/// Issues short-lived JWT access tokens and rotating refresh tokens (hashed in DB).
/// Implements reuse detection: presenting an already-revoked token revokes its whole
/// family (rule X8).
/// </summary>
internal sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;
    private readonly AppDbContext _context;

    public JwtTokenService(IOptions<JwtOptions> options, AppDbContext context)
    {
        _options = options.Value;
        _context = context;
    }

    public (string AccessToken, DateTime ExpiresAtUtc) GenerateAccessToken(
        Guid userId, string email, string fullName, Role role, Guid? classId)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Name, fullName),
            new(ClaimTypes.Role, role.ToString()),
            new(CustomClaims.Role, role.ToString()),
            new(CustomClaims.ClassId, classId?.ToString() ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    public async Task<(string RefreshToken, DateTime ExpiresAtUtc)> GenerateRefreshTokenAsync(
        Guid userId, string? createdByIp, CancellationToken ct = default)
    {
        var (plain, hash) = NewToken();
        var expiresAt = DateTime.UtcNow.AddDays(_options.RefreshTokenDays);

        var token = RefreshToken.Create(userId, hash, expiresAt, createdByIp);
        _context.RefreshTokens.Add(token);
        await _context.SaveChangesAsync(ct);

        return (plain, expiresAt);
    }

    public async Task<RefreshTokenRotation?> RotateRefreshTokenAsync(
        string refreshToken, string? createdByIp, CancellationToken ct = default)
    {
        var hash = HashToken(refreshToken);
        var existing = await _context.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (existing is null || !existing.IsActive)
        {
            // Reuse detection (rule X8): a revoked token presented again ⇒ revoke family.
            if (existing is { IsRevoked: true })
            {
                await RevokeFamilyAsync(existing, ct);
            }

            return null;
        }

        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == existing.UserId, ct);
        if (user is null)
        {
            return null;
        }

        var (newPlain, newHash) = NewToken();
        var newExpiresAt = DateTime.UtcNow.AddDays(_options.RefreshTokenDays);
        var newToken = RefreshToken.Create(user.Id, newHash, newExpiresAt, createdByIp);
        _context.RefreshTokens.Add(newToken);

        existing.Revoke(replacedByTokenHash: newHash);
        await _context.SaveChangesAsync(ct);

        var (access, accessExpires) = GenerateAccessToken(user.Id, user.EmailValue, user.FullName, user.Role, user.ClassId);
        return new RefreshTokenRotation(user.Id, newPlain, newExpiresAt, access, accessExpires);
    }

    public async Task RevokeAllForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var active = await _context.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null && t.ExpiresAtUtc > DateTime.UtcNow)
            .ToListAsync(ct);

        foreach (var token in active)
        {
            token.Revoke();
        }

        await _context.SaveChangesAsync(ct);
    }

    private async Task RevokeFamilyAsync(RefreshToken reused, CancellationToken ct)
    {
        // Revoke the originating token and any descendants (chain via ReplacedByTokenHash).
        var userId = reused.UserId;
        var family = await _context.RefreshTokens
            .Where(t => t.UserId == userId)
            .ToListAsync(ct);

        foreach (var token in family.Where(t => t.IsActive))
        {
            token.Revoke();
        }

        await _context.SaveChangesAsync(ct);
    }

    private static (string plain, string hash) NewToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        var plain = Convert.ToBase64String(bytes);
        return (plain, HashToken(plain));
    }

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

/// <summary>Custom claim types used in addition to the standard JWT claims.</summary>
public static class CustomClaims
{
    public const string Role = "role";
    public const string ClassId = "class_id";
}
