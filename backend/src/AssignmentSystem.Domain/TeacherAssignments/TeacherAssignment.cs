using AssignmentSystem.Domain.ClassCourses;
using AssignmentSystem.Domain.Common;
using AssignmentSystem.Domain.Users;

namespace AssignmentSystem.Domain.TeacherAssignments;

/// <summary>
/// Says who teaches a course offering: a teacher linked to one
/// <see cref="ClassCourse"/>. A real entity rather than a pure join table because the
/// link carries authorization meaning — "this teacher may create assignments and grade
/// submissions for this course in this class". This is the gate for rule B3 (teachers
/// manage only their own assignments) and rule B7 (who may grade).
///
/// It points at the offering rather than carrying its own (course, class) pair: the
/// class↔course relationship belongs to <see cref="ClassCourse"/>, so a teacher can only
/// ever be mapped to a pair the class actually studies, and the two cannot drift apart.
/// </summary>
public sealed class TeacherAssignment : BaseEntity
{
    public Guid TeacherId { get; private set; }
    public ApplicationUser Teacher { get; private set; } = null!;

    public Guid ClassCourseId { get; private set; }
    public ClassCourse ClassCourse { get; private set; } = null!;

    private TeacherAssignment() { }

    public static TeacherAssignment Create(Guid teacherId, Guid classCourseId)
    {
        if (teacherId == Guid.Empty || classCourseId == Guid.Empty)
        {
            throw new DomainException("Teacher and class-course ids are both required.");
        }

        return new TeacherAssignment
        {
            TeacherId = teacherId,
            ClassCourseId = classCourseId,
        };
    }

    /// <summary>True when the given teacher is the owner of this teaching link.</summary>
    public bool IsOwnedBy(Guid teacherId) => TeacherId == teacherId;
}
