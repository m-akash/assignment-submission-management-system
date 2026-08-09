using AssignmentSystem.Domain.Common;

namespace AssignmentSystem.Domain.AcademicYears;

/// <summary>
/// A school session (e.g. "2026-2027"). Students reach it through
/// <see cref="Enrollments.StudentEnrollment"/>: an enrollment names the year it was made
/// for, so a student who repeats a grade or moves up one has a separate row per year and
/// their history stays readable rather than being overwritten.
///
/// The name is admin-supplied rather than derived from the dates. Schools disagree about
/// what to call a session — a January-to-December country writes "2026", a July-to-June one
/// writes "2026-2027" — and inventing the label from the dates would be guessing at that.
/// Uniqueness of the name is what stops the same session being entered twice.
///
/// Exactly one year may be <see cref="IsCurrent"/>. That flag is what the enrollment forms
/// preselect, so leaving two set would make "this year" ambiguous at the one moment it has
/// to be decided. The handlers clear the previous holder and a partial unique index backs
/// the rule up in the database.
/// </summary>
public sealed class AcademicYear : BaseEntity
{
    private const int MaxNameLength = 50;

    /// <summary>The session label, e.g. "2026-2027". Unique, trimmed, never derived.</summary>
    public string Name { get; private set; } = null!;

    /// <summary>First day of the session. A date, not an instant — a session starts on a
    /// calendar day everywhere in the school, regardless of the reader's time zone.</summary>
    public DateOnly StartDate { get; private set; }

    /// <summary>Last day of the session, inclusive. Always after <see cref="StartDate"/>.</summary>
    public DateOnly EndDate { get; private set; }

    /// <summary>Whether this is the session the school is currently running. At most one
    /// year holds this at a time.</summary>
    public bool IsCurrent { get; private set; }

    // Navigation collection (read-only externally; mutated through EF).
    private readonly List<Enrollments.StudentEnrollment> _enrollments = [];
    public IReadOnlyCollection<Enrollments.StudentEnrollment> Enrollments => _enrollments.AsReadOnly();

    private AcademicYear() { }

    public static AcademicYear Create(string name, DateOnly startDate, DateOnly endDate, bool isCurrent)
    {
        Validate(name, startDate, endDate);

        return new AcademicYear
        {
            Name = name.Trim(),
            StartDate = startDate,
            EndDate = endDate,
            IsCurrent = isCurrent,
        };
    }

    public void Update(string name, DateOnly startDate, DateOnly endDate)
    {
        Validate(name, startDate, endDate);

        Name = name.Trim();
        StartDate = startDate;
        EndDate = endDate;
    }

    /// <summary>
    /// Makes this the school's current session. Clearing whoever held it before is the
    /// caller's job — only the handler can see the other rows — which is why this is
    /// separate from <see cref="Update"/> rather than a flag on it.
    /// </summary>
    public void MarkAsCurrent() => IsCurrent = true;

    public void ClearCurrent() => IsCurrent = false;

    private static void Validate(string name, DateOnly startDate, DateOnly endDate)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Academic year name is required.");
        }

        if (name.Trim().Length > MaxNameLength)
        {
            throw new DomainException($"Academic year name cannot exceed {MaxNameLength} characters.");
        }

        if (endDate <= startDate)
        {
            throw new DomainException("The end date must be after the start date.");
        }
    }
}
