using AssignmentSystem.Domain.Classes;
using AssignmentSystem.Domain.Common;
using AssignmentSystem.Domain.Subjects;
using AssignmentSystem.Domain.Users;

namespace AssignmentSystem.Domain.TeacherAssignments;

/// <summary>
/// Resolves the many-to-many-to-many relationship between Teacher, Subject and Class
/// as a real entity (not a pure join table) because the link carries authorization
/// meaning: "this teacher may create assignments and grade submissions for this
/// subject in this class." This is the gate for rule B3 (teachers manage only their
/// own assignments) and rule B7 (who may grade).
/// </summary>
public sealed class TeacherAssignment : BaseEntity
{
    public Guid TeacherId { get; private set; }
    public ApplicationUser Teacher { get; private set; } = null!;

    public Guid SubjectId { get; private set; }
    public Subject Subject { get; private set; } = null!;

    public Guid ClassId { get; private set; }
    public Class Class { get; private set; } = null!;

    private TeacherAssignment() { }

    public static TeacherAssignment Create(Guid teacherId, Guid subjectId, Guid classId)
    {
        if (teacherId == Guid.Empty || subjectId == Guid.Empty || classId == Guid.Empty)
        {
            throw new DomainException("Teacher, subject and class ids are all required.");
        }

        return new TeacherAssignment
        {
            TeacherId = teacherId,
            SubjectId = subjectId,
            ClassId = classId,
        };
    }

    /// <summary>True when the given teacher is the owner of this assignment link.</summary>
    public bool IsOwnedBy(Guid teacherId) => TeacherId == teacherId;
}
