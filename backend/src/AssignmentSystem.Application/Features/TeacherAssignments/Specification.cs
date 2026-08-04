using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.TeacherAssignments;

namespace AssignmentSystem.Application.Features.TeacherAssignments;

internal sealed class TeacherAssignmentWithDetailsSpecification : Specification<TeacherAssignment>
{
    public TeacherAssignmentWithDetailsSpecification(Guid id)
    {
        Criteria = ta => ta.Id == id;
        AddInclude(ta => ta.Teacher);
        AddInclude("ClassCourse.Class");
        AddInclude("ClassCourse.Course");
    }
}

internal sealed class TeacherAssignmentDuplicateSpecification : Specification<TeacherAssignment>
{
    public TeacherAssignmentDuplicateSpecification(Guid teacherId, Guid classCourseId)
    {
        Criteria = ta => ta.TeacherId == teacherId && ta.ClassCourseId == classCourseId;
    }
}

internal sealed class TeacherAssignmentsPagedSpecification : Specification<TeacherAssignment>
{
    public TeacherAssignmentsPagedSpecification(
        Guid? teacherId, Guid? courseId, Guid? classId, Guid? classCourseId, string? search, int page, int pageSize)
    {
        ApplyNoTracking();
        AddInclude(ta => ta.Teacher);
        AddInclude("ClassCourse.Class");
        AddInclude("ClassCourse.Course");
        ApplyOrderBy(ta => ta.Teacher.FullName);
        ApplyPaging(page, pageSize);

        var searchLower = search?.Trim().ToLowerInvariant();

        // ToLower() (not ToLowerInvariant()) below: this Criteria is an expression tree that EF
        // Core translates to SQL LOWER(...), which ToLowerInvariant() cannot be translated to.
        // The column value never touches client culture, so the CA1304/CA1311 concern doesn't apply.
#pragma warning disable CA1304, CA1311
        Criteria = ta =>
            (!teacherId.HasValue || ta.TeacherId == teacherId.Value) &&
            (!classCourseId.HasValue || ta.ClassCourseId == classCourseId.Value) &&
            (!courseId.HasValue || ta.ClassCourse.CourseId == courseId.Value) &&
            (!classId.HasValue || ta.ClassCourse.ClassId == classId.Value) &&
            (string.IsNullOrWhiteSpace(searchLower) ||
             ta.Teacher.FullName.ToLower().Contains(searchLower) ||
             ta.ClassCourse.Course.Name.ToLower().Contains(searchLower) ||
             ta.ClassCourse.Class.Name.ToLower().Contains(searchLower));
#pragma warning restore CA1304, CA1311
    }
}
