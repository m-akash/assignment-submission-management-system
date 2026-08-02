using AssignmentSystem.Domain.Common;

namespace AssignmentSystem.Domain.Classes;

/// <summary>
/// A class/course (e.g. "Grade 10 - Section A"). Students belong to exactly one
/// class; teachers are linked to classes via <see cref="TeacherAssignments.TeacherAssignment"/>.
/// Named <c>Class</c> deliberately — it is the domain term. Persisted as table
/// <c>classes</c> to avoid the SQL reserved word.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1716:Identifiers should not match keywords",
    Justification = "'Class' is the correct domain term for a school class/course.")]
public sealed class Class : BaseEntity
{
    public string Name { get; private set; } = null!;
    public string? Grade { get; private set; }
    public string? Section { get; private set; }

    // Navigation collections (read-only externally; mutated through methods).
    private readonly List<Users.ApplicationUser> _students = [];
    public IReadOnlyCollection<Users.ApplicationUser> Students => _students.AsReadOnly();

    private Class() { }

    public static Class Create(string name, string? grade = null, string? section = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Class name is required.");
        }

        if (name.Length > 150)
        {
            throw new DomainException("Class name cannot exceed 150 characters.");
        }

        return new Class
        {
            Name = name.Trim(),
            Grade = string.IsNullOrWhiteSpace(grade) ? null : grade.Trim(),
            Section = string.IsNullOrWhiteSpace(section) ? null : section.Trim(),
        };
    }

    public void Update(string name, string? grade, string? section)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Class name is required.");
        }

        Name = name.Trim();
        Grade = string.IsNullOrWhiteSpace(grade) ? null : grade.Trim();
        Section = string.IsNullOrWhiteSpace(section) ? null : section.Trim();
    }
}
