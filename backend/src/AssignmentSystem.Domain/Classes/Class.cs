using AssignmentSystem.Domain.Common;

namespace AssignmentSystem.Domain.Classes;

/// <summary>
/// A class cohort — a grade and a section, kept as two separate values. Students join through
/// <see cref="Enrollments.StudentEnrollment"/>, the courses the class studies through
/// <see cref="ClassCourses.ClassCourse"/>, and teachers reach it through the offering via
/// <see cref="TeacherAssignments.TeacherAssignment"/>.
/// Named <c>Class</c> deliberately — it is the domain term. Persisted as table
/// <c>classes</c> to avoid the SQL reserved word.
///
/// There is no composed name column. The grade is a number and the section is a letter, and
/// every caller receives them as two fields: a UI picks a grade and then a section, a list
/// gives each its own column, and sorting is arithmetic rather than string parsing. A single
/// stored "Class IX - Section A" could only disagree with the pair it was built from.
///
/// A grade may hold any number of sections, but only one cohort per (grade, section) —
/// enforced by the handlers and backed by a unique index.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1716:Identifiers should not match keywords",
    Justification = "'Class' is the correct domain term for a school class/course.")]
public sealed class Class : BaseEntity
{
    private const int MinLevel = 1;
    private const int MaxLevel = 12;
    private const int MaxSectionLength = 50;

    /// <summary>Grade as a number, 1..12 — rendered as the number itself, never a numeral.</summary>
    public int Level { get; private set; }

    /// <summary>Required. Nullable only because rows predating that rule may still hold NULL.</summary>
    public string? Section { get; private set; }

    /// <summary>
    /// A one-line rendering for prose only — email subjects and bodies, where a grade and a
    /// section cannot be two fields. Derived, never stored, and never returned by the API:
    /// every DTO carries <see cref="Level"/> and <see cref="Section"/> separately so screens
    /// can lay them out themselves.
    /// </summary>
    public string DisplayName => string.IsNullOrWhiteSpace(Section)
        ? $"Class {Level}"
        : $"Class {Level} - Section {Section}";

    // Navigation collections (read-only externally; mutated through methods).
    private readonly List<Enrollments.StudentEnrollment> _enrollments = [];
    public IReadOnlyCollection<Enrollments.StudentEnrollment> Enrollments => _enrollments.AsReadOnly();

    private readonly List<ClassCourses.ClassCourse> _classCourses = [];
    public IReadOnlyCollection<ClassCourses.ClassCourse> ClassCourses => _classCourses.AsReadOnly();

    private Class() { }

    public static Class Create(int level, string section)
    {
        Validate(level, section);

        return new Class
        {
            Level = level,
            Section = section.Trim(),
        };
    }

    public void Update(int level, string section)
    {
        Validate(level, section);

        Level = level;
        Section = section.Trim();
    }

    private static void Validate(int level, string section)
    {
        if (level is < MinLevel or > MaxLevel)
        {
            throw new DomainException($"Class level must be between {MinLevel} and {MaxLevel}.");
        }

        if (string.IsNullOrWhiteSpace(section))
        {
            throw new DomainException("Section is required.");
        }

        if (section.Trim().Length > MaxSectionLength)
        {
            throw new DomainException($"Section cannot exceed {MaxSectionLength} characters.");
        }
    }
}
