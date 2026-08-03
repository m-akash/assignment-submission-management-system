using AssignmentSystem.Domain.Common;

namespace AssignmentSystem.Domain.Departments;

/// <summary>
/// An organisational unit that staff belong to and courses are grouped under, e.g.
/// "Science" (code "SCI") covering Physics, Chemistry and Biology.
///
/// Deliberately separate from <see cref="Courses.Course"/>: a department employs
/// teachers, a course is a unit of study, and one department owns many courses. The
/// short <see cref="Code"/> is what teacher ids are built from ("INS-SCI-01"), so it is
/// stored as its own value rather than parsed back out of a course code.
/// </summary>
public sealed class Department : BaseEntity
{
    public string Name { get; private set; } = null!;
    public string Code { get; private set; } = null!;

    private Department() { }

    public static Department Create(string name, string code)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Department name is required.");
        }

        if (name.Length > 150)
        {
            throw new DomainException("Department name cannot exceed 150 characters.");
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainException("Department code is required.");
        }

        // Teacher ids embed this code, and that column is bounded — keep it short.
        if (code.Trim().Length > 10)
        {
            throw new DomainException("Department code cannot exceed 10 characters.");
        }

        return new Department
        {
            Name = name.Trim(),
            Code = code.Trim().ToUpperInvariant(),
        };
    }

    public void Update(string name, string code)
    {
        var updated = Create(name, code);
        Name = updated.Name;
        Code = updated.Code;
    }
}
