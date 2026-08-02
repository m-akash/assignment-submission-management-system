using AssignmentSystem.Application.Abstractions;
using AssignmentSystem.Domain.Users;
using Microsoft.AspNetCore.Identity;

namespace AssignmentSystem.Infrastructure.Identity;

/// <summary>
/// Adapter over ASP.NET Core's <see cref="PasswordHasher{TUser}"/> (PBKDF2). Exposed
/// to handlers through the Application-owned <see cref="IPasswordHasher"/> port so
/// the scheme is swappable (e.g. to Argon2) without touching call sites.
/// </summary>
internal sealed class PasswordHasherAdapter : IPasswordHasher
{
    private readonly PasswordHasher<ApplicationUser> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(null!, password);

    public bool Verify(string passwordHash, string providedPassword) =>
        _hasher.VerifyHashedPassword(null!, passwordHash, providedPassword)
            is PasswordVerificationResult.Success
            or PasswordVerificationResult.SuccessRehashNeeded;
}
