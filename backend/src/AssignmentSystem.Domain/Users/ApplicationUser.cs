using AssignmentSystem.Domain.Classes;
using AssignmentSystem.Domain.Common;
using AssignmentSystem.Domain.Enums;

namespace AssignmentSystem.Domain.Users;

/// <summary>
/// Application user — all three roles (Admin, Teacher, Student) in one table,
/// discriminated by <see cref="Role"/>. Password hash is stored (never the plain
/// password); students carry a <see cref="ClassId"/>. Supports soft delete + activation.
/// </summary>
public sealed class ApplicationUser : BaseEntity, ISoftDeletable
{
    public Email Email { get; private set; } = null!;
    public string EmailValue => Email.Value;

    public string FullName { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public Role Role { get; private set; }

    /// <summary>Class id — only meaningful for students (null for admin/teacher).</summary>
    public Guid? ClassId { get; private set; }
    public Class? Class { get; private set; }

    public bool IsActive { get; private set; } = true;

    // ── Soft delete ───────────────────────────────────────────────────────────
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }

    // ── Refresh tokens (multi-device) ─────────────────────────────────────────
    private readonly List<RefreshToken> _refreshTokens = [];
    public IReadOnlyCollection<RefreshToken> RefreshTokens => _refreshTokens.AsReadOnly();

    private ApplicationUser() { }

    public static ApplicationUser Create(
        string email,
        string fullName,
        string passwordHash,
        Role role,
        Guid? classId = null)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new DomainException("Full name is required.");
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainException("Password hash is required.");
        }

        // A student must belong to a class; admins/teachers must not.
        if (role == Role.Student && classId is null)
        {
            throw new DomainException("A student must be assigned to a class.");
        }

        if (role != Role.Student && classId is not null)
        {
            throw new DomainException("Only students may be assigned to a class.");
        }

        return new ApplicationUser
        {
            Email = Email.Create(email),
            FullName = fullName.Trim(),
            PasswordHash = passwordHash,
            Role = role,
            ClassId = classId,
            IsActive = true,
        };
    }

    public void UpdateProfile(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new DomainException("Full name is required.");
        }

        FullName = fullName.Trim();
    }

    public void SetPasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainException("Password hash is required.");
        }

        PasswordHash = passwordHash;
    }

    public void AssignToClass(Guid classId)
    {
        if (Role != Role.Student)
        {
            throw new DomainException("Only students may be assigned to a class.");
        }

        if (classId == Guid.Empty)
        {
            throw new DomainException("A valid class id is required.");
        }

        ClassId = classId;
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;

    public void SoftDelete(DateTime deletedAtUtc)
    {
        IsDeleted = true;
        DeletedAtUtc = deletedAtUtc;
        IsActive = false;
    }
}
