using AssignmentSystem.Domain.Common;
using AssignmentSystem.Domain.Departments;
using AssignmentSystem.Domain.Groups;

namespace AssignmentSystem.Domain.Courses;

/// <summary>
/// A course (e.g. "Mathematics", code "MATH101") — a unit of study offered to a class.
/// Belongs to exactly one <see cref="Departments.Department"/>, which is the
/// organisational unit that staffs it. Linked to teachers via
/// <see cref="TeacherAssignments.TeacherAssignment"/> and to the assignments created
/// for it.
/// </summary>
public sealed class Course : BaseEntity
{
    public string Name { get; private set; } = null!;
    public string Code { get; private set; } = null!;

    /// <summary>The department that owns this course — required; one department has many courses.</summary>
    public Guid DepartmentId { get; private set; }
    public Department Department { get; private set; } = null!;

    /// <summary>
    /// The group this course is restricted to, e.g. "Physics" → Science. Null means the
    /// course is open to everyone in the class — most courses at class VI–VIII (which
    /// have no groups) and shared subjects like English are null.
    ///
    /// This is where a group actually does something: <see cref="ApplicationUser.GroupId"/>
    /// on its own is just a profile fact, but pairing it against this field is what
    /// decides whether a Business Studies student can see a Science-only assignment.
    /// </summary>
    public Guid? GroupId { get; private set; }
    public Group? Group { get; private set; }

    private Course() { }

    public static Course Create(string name, string code, Guid departmentId, Guid? groupId = null)
    {
        Validate(name, code, departmentId);

        return new Course
        {
            Name = name.Trim(),
            Code = code.Trim().ToUpperInvariant(),
            DepartmentId = departmentId,
            GroupId = groupId,
        };
    }

    public void Update(string name, string code, Guid departmentId, Guid? groupId = null)
    {
        Validate(name, code, departmentId);

        Name = name.Trim();
        Code = code.Trim().ToUpperInvariant();
        DepartmentId = departmentId;
        GroupId = groupId;
    }

    private static void Validate(string name, string code, Guid departmentId)
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

        if (departmentId == Guid.Empty)
        {
            throw new DomainException("A course must belong to a department.");
        }
    }
}
