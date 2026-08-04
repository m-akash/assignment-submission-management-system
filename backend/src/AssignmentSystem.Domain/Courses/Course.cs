using AssignmentSystem.Domain.Common;

namespace AssignmentSystem.Domain.Courses;

/// <summary>
/// A course (e.g. "Mathematics", code "MATH101") — a unit of study offered to a class.
/// Linked to teachers via <see cref="TeacherAssignments.TeacherAssignment"/> and to the
/// assignments created for it.
/// </summary>
public sealed class Course : BaseEntity
{
    public string Name { get; private set; } = null!;
    public string Code { get; private set; } = null!;

    private Course() { }

    public static Course Create(string name, string code)
    {
        Validate(name, code);

        return new Course
        {
            Name = name.Trim(),
            Code = code.Trim().ToUpperInvariant(),
        };
    }

    public void Update(string name, string code)
    {
        Validate(name, code);

        Name = name.Trim();
        Code = code.Trim().ToUpperInvariant();
    }

    private static void Validate(string name, string code)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Course name is required.");
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainException("Course code is required.");
        }

        if (code.Trim().Length > 30)
        {
            throw new DomainException("Course code cannot exceed 30 characters.");
        }
    }
}
