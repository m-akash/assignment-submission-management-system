using AssignmentSystem.Domain.Common;

namespace AssignmentSystem.Domain.Classes;

/// <summary>
/// A class cohort (e.g. "Class IX - Section A"). Students belong to exactly one class;
/// teachers are linked to classes via <see cref="TeacherAssignments.TeacherAssignment"/>.
/// Named <c>Class</c> deliberately — it is the domain term. Persisted as table
/// <c>classes</c> to avoid the SQL reserved word.
///
/// The grade is stored as a number and rendered as a Roman numeral. Keeping the numeral
/// out of storage leaves one source of truth: ordering and the "does this grade have
/// groups?" rule become arithmetic rather than string parsing.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1716:Identifiers should not match keywords",
    Justification = "'Class' is the correct domain term for a school class/course.")]
public sealed class Class : BaseEntity
{
    private const int MinLevel = 1;
    private const int MaxLevel = 12;

    private static readonly string[] RomanNumerals =
        ["I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI", "XII"];

    public string Name { get; private set; } = null!;

    /// <summary>Grade as a number, 1..12. Rendered through <see cref="GradeLabel"/>.</summary>
    public int Level { get; private set; }

    public string? Section { get; private set; }

    /// <summary>The grade in Roman numerals ("IX") — what the school calls it, and what
    /// student ids are built from.</summary>
    public string GradeLabel => RomanNumerals[Level - 1];

    // Navigation collections (read-only externally; mutated through methods).
    private readonly List<Users.ApplicationUser> _students = [];
    public IReadOnlyCollection<Users.ApplicationUser> Students => _students.AsReadOnly();

    private Class() { }

    public static Class Create(string name, int level, string? section = null)
    {
        Validate(name, level);

        return new Class
        {
            Name = name.Trim(),
            Level = level,
            Section = string.IsNullOrWhiteSpace(section) ? null : section.Trim(),
        };
    }

    public void Update(string name, int level, string? section)
    {
        Validate(name, level);

        Name = name.Trim();
        Level = level;
        Section = string.IsNullOrWhiteSpace(section) ? null : section.Trim();
    }

    private static void Validate(string name, int level)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Class name is required.");
        }

        if (name.Length > 150)
        {
            throw new DomainException("Class name cannot exceed 150 characters.");
        }

        if (level is < MinLevel or > MaxLevel)
        {
            throw new DomainException($"Class level must be between {MinLevel} and {MaxLevel}.");
        }
    }
}
