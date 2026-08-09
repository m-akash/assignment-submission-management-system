using AssignmentSystem.Domain.AcademicYears;
using AssignmentSystem.Domain.Classes;
using AssignmentSystem.Domain.Common;
using AssignmentSystem.Domain.Users;

namespace AssignmentSystem.Domain.Enrollments;

/// <summary>
/// A student's membership of a class for one academic year. Modelled as its own row rather
/// than a <c>users.class_id</c> column so a student can sit in more than one class (a
/// repeated grade, an elective cohort, a mid-year transfer where both memberships must
/// remain visible) and so the moment they joined is recorded.
///
/// The academic year is part of the row rather than of the class: cohorts outlive sessions,
/// so "Class IX - Section A" is the same cohort every year and it is the enrollment that
/// says which year a student sat in it. That is what lets the same student appear in Class
/// IX for 2025-2026 and Class X for 2026-2027 with both memberships intact, and what makes
/// a repeated grade expressible at all — the same (student, class) pair in two years.
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

    public Guid AcademicYearId { get; private set; }
    public AcademicYear AcademicYear { get; private set; } = null!;

    public DateTime EnrolledAtUtc { get; private set; }

    private StudentEnrollment() { }

    public static StudentEnrollment Create(Guid studentId, Guid classId, Guid academicYearId, DateTime enrolledAtUtc)
    {
        if (studentId == Guid.Empty || classId == Guid.Empty || academicYearId == Guid.Empty)
        {
            throw new DomainException("Student, class and academic year ids are all required.");
        }

        return new StudentEnrollment
        {
            StudentId = studentId,
            ClassId = classId,
            AcademicYearId = academicYearId,
            EnrolledAtUtc = enrolledAtUtc,
        };
    }
}
