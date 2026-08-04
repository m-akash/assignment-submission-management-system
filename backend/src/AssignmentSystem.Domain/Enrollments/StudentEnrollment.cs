using AssignmentSystem.Domain.Classes;
using AssignmentSystem.Domain.Common;
using AssignmentSystem.Domain.Users;

namespace AssignmentSystem.Domain.Enrollments;

/// <summary>
/// A student's membership of a class. Modelled as its own row rather than a
/// <c>users.class_id</c> column so a student can sit in more than one class (a repeated
/// grade, an elective cohort, a mid-year transfer where both memberships must remain
/// visible) and so the moment they joined is recorded.
///
/// This is the gate for rule B1: a student may only see and submit to assignments whose
/// offering belongs to a class they are enrolled in.
/// </summary>
public sealed class StudentEnrollment : BaseEntity
{
    public Guid StudentId { get; private set; }
    public ApplicationUser Student { get; private set; } = null!;

    public Guid ClassId { get; private set; }
    public Class Class { get; private set; } = null!;

    public DateTime EnrolledAtUtc { get; private set; }

    private StudentEnrollment() { }

    public static StudentEnrollment Create(Guid studentId, Guid classId, DateTime enrolledAtUtc)
    {
        if (studentId == Guid.Empty || classId == Guid.Empty)
        {
            throw new DomainException("Student and class ids are both required.");
        }

        return new StudentEnrollment
        {
            StudentId = studentId,
            ClassId = classId,
            EnrolledAtUtc = enrolledAtUtc,
        };
    }
}
