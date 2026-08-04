using AssignmentSystem.Domain.Classes;
using AssignmentSystem.Domain.Common;
using AssignmentSystem.Domain.Courses;

namespace AssignmentSystem.Domain.ClassCourses;

/// <summary>
/// A course offering: "this class studies this course". The catalogue row that makes
/// the class↔course pair a first-class thing rather than something inferred from
/// whichever teacher happens to be mapped.
///
/// Everything downstream hangs off it — a <see cref="TeacherAssignments.TeacherAssignment"/>
/// says who teaches an offering, and an <see cref="Assignments.Assignment"/> is scoped to
/// one. That is what stops an admin from mapping a teacher to a (class, course) pair the
/// class does not actually study, and it means an assignment's class and course can never
/// disagree with each other: there is one column, not two.
/// </summary>
public sealed class ClassCourse : BaseEntity
{
    public Guid ClassId { get; private set; }
    public Class Class { get; private set; } = null!;

    public Guid CourseId { get; private set; }
    public Course Course { get; private set; } = null!;

    private ClassCourse() { }

    public static ClassCourse Create(Guid classId, Guid courseId)
    {
        if (classId == Guid.Empty || courseId == Guid.Empty)
        {
            throw new DomainException("Class and course ids are both required.");
        }

        return new ClassCourse
        {
            ClassId = classId,
            CourseId = courseId,
        };
    }
}
