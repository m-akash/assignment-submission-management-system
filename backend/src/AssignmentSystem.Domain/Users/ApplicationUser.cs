using AssignmentSystem.Domain.Classes;
using AssignmentSystem.Domain.Common;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Domain.Departments;

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

    /// <summary>
    /// Human-readable school id, e.g. "10-A-003" (class grade - section - sequence).
    /// Only meaningful for students (null for admin/teacher). The caller computes the
    /// value (it needs a repository lookup for the next sequence number, which the
    /// domain can't do) — this entity just enforces that a student has one and nobody
    /// else does.
    /// </summary>
    public string? StudentId { get; private set; }

    /// <summary>The organisational unit a teacher belongs to. Only meaningful for
    /// teachers (null for admin/student).</summary>
    public Guid? DepartmentId { get; private set; }
    public Department? Department { get; private set; }

    /// <summary>
    /// Human-readable staff id, e.g. "INS-PHY-01" (Instructor - department code -
    /// sequence within that department). Only meaningful for teachers. Same shape as
    /// <see cref="StudentId"/>: the caller computes the value.
    /// </summary>
    public string? TeacherId { get; private set; }

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
        Guid? classId = null,
        string? studentId = null,
        Guid? departmentId = null,
        string? teacherId = null)
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

        // Same shape as the class rule above: students must have a student id, and
        // only students may.
        if (role == Role.Student && string.IsNullOrWhiteSpace(studentId))
        {
            throw new DomainException("A student must have a student id.");
        }

        if (role != Role.Student && studentId is not null)
        {
            throw new DomainException("Only students may have a student id.");
        }

        // Same shape again: a teacher must have a department and a teacher id, and
        // only teachers may.
        if (role == Role.Teacher && departmentId is null)
        {
            throw new DomainException("A teacher must be assigned to a department.");
        }

        if (role != Role.Teacher && departmentId is not null)
        {
            throw new DomainException("Only teachers may be assigned to a department.");
        }

        if (role == Role.Teacher && string.IsNullOrWhiteSpace(teacherId))
        {
            throw new DomainException("A teacher must have a teacher id.");
        }

        if (role != Role.Teacher && teacherId is not null)
        {
            throw new DomainException("Only teachers may have a teacher id.");
        }

        return new ApplicationUser
        {
            Email = Email.Create(email),
            FullName = fullName.Trim(),
            PasswordHash = passwordHash,
            Role = role,
            ClassId = classId,
            StudentId = studentId?.Trim(),
            DepartmentId = departmentId,
            TeacherId = teacherId?.Trim(),
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

    /// <summary>Reassigns a teacher's department. Does not regenerate <see cref="TeacherId"/>
    /// — same trade-off as <see cref="AssignToClass"/> not regenerating <see cref="StudentId"/>.</summary>
    public void AssignToDepartment(Guid departmentId)
    {
        if (Role != Role.Teacher)
        {
            throw new DomainException("Only teachers may be assigned to a department.");
        }

        if (departmentId == Guid.Empty)
        {
            throw new DomainException("A valid department id is required.");
        }

        DepartmentId = departmentId;
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
