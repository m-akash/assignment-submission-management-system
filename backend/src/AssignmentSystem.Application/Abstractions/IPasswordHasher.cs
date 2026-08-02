namespace AssignmentSystem.Application.Abstractions;

/// <summary>
/// Password hashing port. Wraps ASP.NET Core's PBKDF2 hasher behind an Application-
/// owned interface so the hashing scheme is swappable (e.g. to Argon2) without
/// touching handlers.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string passwordHash, string providedPassword);
}
