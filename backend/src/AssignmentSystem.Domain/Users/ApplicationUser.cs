using AssignmentSystem.Domain.Common;
using AssignmentSystem.Domain.Enrollments;
using AssignmentSystem.Domain.Enums;

namespace AssignmentSystem.Domain.Users;

/// <summary>
/// Application user — all three roles (Admin, Teacher, Student) in one table,
/// discriminated by <see cref="Role"/>. Password hash is stored (never the plain
/// password). Class membership is not a column here: a student's classes are
/// <see cref="StudentEnrollment"/> rows, so one student can belong to several.
/// Supports soft delete + activation.
/// </summary>
public sealed class ApplicationUser : BaseEntity, ISoftDeletable
{
    public Email Email { get; private set; } = null!;
    public string EmailValue => Email.Value;

    public string FullName { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public Role Role { get; private set; }

    /// <summary>
    /// Human-readable school id, e.g. "10-A-003" (class grade - section - sequence).
    /// Only meaningful for students (null for admin/teacher). The caller computes the
    /// value (it needs a repository lookup for the next sequence number, which the
    /// domain can't do) — this entity just enforces that a student has one and nobody
    /// else does.
    /// </summary>
    public string? StudentId { get; private set; }

    /// <summary>
    /// Human-readable staff id, e.g. "INS-01" (Instructor - sequence). Only meaningful
    /// for teachers. Same shape as <see cref="StudentId"/>: the caller computes the value.
    /// </summary>
    public string? TeacherId { get; private set; }

    public bool IsActive { get; private set; } = true;

    // ── Soft delete ───────────────────────────────────────────────────────────
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }

    // ── Refresh tokens (multi-device) ─────────────────────────────────────────
    private readonly List<RefreshToken> _refreshTokens = [];
    public IReadOnlyCollection<RefreshToken> RefreshTokens => _refreshTokens.AsReadOnly();

    // ── Class membership (students only) ──────────────────────────────────────
    private readonly List<StudentEnrollment> _enrollments = [];
    public IReadOnlyCollection<StudentEnrollment> Enrollments => _enrollments.AsReadOnly();

    private ApplicationUser() { }

    public static ApplicationUser Create(
        string email,
        string fullName,
        string passwordHash,
        Role role,
        string? studentId = null,
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

        // Students must have a student id, and only students may. The class they sit in
        // is a StudentEnrollment row, created alongside the user by the handler — this
        // entity cannot enforce "a student has at least one class" on its own, so
        // CreateUserHandler does (and the validator refuses the request without one).
        if (role == Role.Student && string.IsNullOrWhiteSpace(studentId))
        {
            throw new DomainException("A student must have a student id.");
        }

        if (role != Role.Student && studentId is not null)
        {
            throw new DomainException("Only students may have a student id.");
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
            StudentId = studentId?.Trim(),
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

    /// <summary>
    /// Enrols this student into a class. Idempotent — re-enrolling into a class they are
    /// already in is a no-op rather than an error, so a repeated admin action cannot
    /// create a duplicate the unique index would then reject.
    /// </summary>
    public StudentEnrollment EnrollIn(Guid classId, DateTime enrolledAtUtc)
    {
        if (Role != Role.Student)
        {
            throw new DomainException("Only students may be enrolled in a class.");
        }

        if (classId == Guid.Empty)
        {
            throw new DomainException("A valid class id is required.");
        }

        var existing = _enrollments.Find(e => e.ClassId == classId);
        if (existing is not null)
        {
            return existing;
        }

        var enrollment = StudentEnrollment.Create(Id, classId, enrolledAtUtc);
        _enrollments.Add(enrollment);
        return enrollment;
    }

    /// <summary>True when the student is enrolled in the given class (rule B1).</summary>
    public bool IsEnrolledIn(Guid classId) => _enrollments.Exists(e => e.ClassId == classId);

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;

    public void SoftDelete(DateTime deletedAtUtc)
    {
        IsDeleted = true;
        DeletedAtUtc = deletedAtUtc;
        IsActive = false;
    }
}
