using AssignmentSystem.Domain.Common;

namespace AssignmentSystem.Domain.Subjects;

/// <summary>
/// A subject (e.g. "Mathematics", code "MATH101"). Linked to teachers via
/// <see cref="TeacherAssignments.TeacherAssignment"/> and to the assignments
/// created for it.
/// </summary>
public sealed class Subject : BaseEntity
{
    public string Name { get; private set; } = null!;
    public string Code { get; private set; } = null!;

    private Subject() { }

    public static Subject Create(string name, string code)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Subject name is required.");
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainException("Subject code is required.");
        }

        if (code.Length > 30)
        {
            throw new DomainException("Subject code cannot exceed 30 characters.");
        }

        return new Subject
        {
            Name = name.Trim(),
            Code = code.Trim().ToUpperInvariant(),
        };
    }

    public void Update(string name, string code)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Subject name is required.");
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainException("Subject code is required.");
        }

        Name = name.Trim();
        Code = code.Trim().ToUpperInvariant();
    }
}
