using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.TeacherAssignments;

namespace AssignmentSystem.Application.Features.StudentCourses;

/// <summary>
/// Every <see cref="TeacherAssignment"/> for a set of class ids — the classes a student is
/// enrolled in. Includes the teacher and the offering's class and course so the handler can
/// build the flattened <see cref="StudentCourseDto"/> without a second query.
///
/// Deliberately unpaged: an offering can have several teachers, and the feature shows one
/// row per offering, so paging is applied in the handler after grouping — otherwise an
/// offering split across a page boundary would either repeat or vanish.
/// </summary>
internal sealed class StudentCoursesByClassesSpecification : Specification<TeacherAssignment>
{
    public StudentCoursesByClassesSpecification(IReadOnlyCollection<Guid> classIds)
    {
        ApplyNoTracking();
        AddInclude(ta => ta.Teacher);
        AddInclude("ClassCourse.Class");
        AddInclude("ClassCourse.Course");

        Criteria = ta => classIds.Contains(ta.ClassCourse.ClassId);
    }
}
