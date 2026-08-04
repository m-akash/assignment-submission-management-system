using AssignmentSystem.Domain.Common;

namespace AssignmentSystem.Domain.Groups;

/// <summary>
/// An academic stream a student chooses from class IX onward — Science, Humanities or
/// Business Studies. Students in the same class can be in different groups, so this sits
/// on the student rather than on the class.
///
/// Deliberately not the same thing as <see cref="Departments.Department"/>: a department
/// is where a teacher works and includes units students never pick (Languages,
/// Mathematics), while a group is the small fixed set a student can belong to.
/// </summary>
public sealed class Group : BaseEntity
{
    public string Name { get; private set; } = null!;
    public string Code { get; private set; } = null!;

    private Group() { }

    public static Group Create(string name, string code)
    {
        Validate(name, code);

        return new Group
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
            throw new DomainException("Group name is required.");
        }

        if (name.Length > 150)
        {
            throw new DomainException("Group name cannot exceed 150 characters.");
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainException("Group code is required.");
        }

        if (code.Trim().Length > 10)
        {
            throw new DomainException("Group code cannot exceed 10 characters.");
        }
    }
}
