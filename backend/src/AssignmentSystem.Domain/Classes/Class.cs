using AssignmentSystem.Domain.Common;

namespace AssignmentSystem.Domain.Classes;

/// <summary>
/// A class cohort (e.g. "Class IX - Section A"). Students join through
/// <see cref="Enrollments.StudentEnrollment"/>, the courses the class studies through
/// <see cref="ClassCourses.ClassCourse"/>, and teachers reach it through the offering via
/// <see cref="TeacherAssignments.TeacherAssignment"/>.
/// Named <c>Class</c> deliberately — it is the domain term. Persisted as table
/// <c>classes</c> to avoid the SQL reserved word.
///
/// The grade is stored as a number and rendered as a Roman numeral. Keeping the numeral
/// out of storage leaves one source of truth: ordering and the "does this grade have
/// groups?" rule become arithmetic rather than string parsing.
///
/// A grade may hold any number of sections, but only one cohort per (grade, section) —
/// enforced by the handlers and backed by a unique index. The name is composed from the
/// two, so uniqueness of the pair makes the name unique too.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1716:Identifiers should not match keywords",
    Justification = "'Class' is the correct domain term for a school class/course.")]
public sealed class Class : BaseEntity
{
    private const int MinLevel = 1;
    private const int MaxLevel = 12;
    private const int MaxSectionLength = 50;

    private static readonly string[] RomanNumerals =
        ["I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI", "XII"];

    /// <summary>The display name, always derived from the grade and section — never supplied
    /// by a caller. See <see cref="BuildName"/>.</summary>
    public string Name { get; private set; } = null!;

    /// <summary>Grade as a number, 1..12. Rendered through <see cref="GradeLabel"/>.</summary>
    public int Level { get; private set; }

    /// <summary>Required. Nullable only because rows predating that rule may still hold NULL.</summary>
    public string? Section { get; private set; }

    /// <summary>The grade in Roman numerals ("IX") — what the school calls it, and what
    /// student ids are built from.</summary>
    public string GradeLabel => RomanNumerals[Level - 1];

    // Navigation collections (read-only externally; mutated through methods).
    private readonly List<Enrollments.StudentEnrollment> _enrollments = [];
    public IReadOnlyCollection<Enrollments.StudentEnrollment> Enrollments => _enrollments.AsReadOnly();

    private readonly List<ClassCourses.ClassCourse> _classCourses = [];
    public IReadOnlyCollection<ClassCourses.ClassCourse> ClassCourses => _classCourses.AsReadOnly();

    private Class() { }

    public static Class Create(int level, string section)
    {
        Validate(level, section);
        var trimmed = section.Trim();

        return new Class
        {
            Level = level,
            Section = trimmed,
            Name = BuildName(level, trimmed),
        };
    }

    public void Update(int level, string section)
    {
        Validate(level, section);
        var trimmed = section.Trim();

        Level = level;
        Section = trimmed;
        Name = BuildName(level, trimmed);
    }

    /// <summary>
    /// The one place a class name is composed. Admins supply only the grade and the section;
    /// the "Class" and "Section" words are ours, so every cohort reads the same way and the
    /// name can never drift from the grade and section it describes.
    /// </summary>
    private static string BuildName(int level, string section) =>
        $"Class {RomanNumerals[level - 1]} - Section {section}";

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
