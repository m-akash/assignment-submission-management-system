using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.TeacherAssignments;

namespace AssignmentSystem.Application.Features.TeacherAssignments;

internal sealed class TeacherAssignmentWithDetailsSpecification : Specification<TeacherAssignment>
{
    public TeacherAssignmentWithDetailsSpecification(Guid id)
    {
        Criteria = ta => ta.Id == id;
        AddInclude(ta => ta.Teacher);
        AddInclude(ta => ta.Course);
        AddInclude(ta => ta.Class);
    }
}

internal sealed class TeacherAssignmentDuplicateSpecification : Specification<TeacherAssignment>
{
    public TeacherAssignmentDuplicateSpecification(Guid teacherId, Guid courseId, Guid classId)
    {
        Criteria = ta => ta.TeacherId == teacherId && ta.CourseId == courseId && ta.ClassId == classId;
    }
}

internal sealed class TeacherAssignmentsPagedSpecification : Specification<TeacherAssignment>
{
    public TeacherAssignmentsPagedSpecification(
        Guid? teacherId, Guid? courseId, Guid? classId, string? search, int page, int pageSize)
    {
        ApplyNoTracking();
        AddInclude(ta => ta.Teacher);
        AddInclude(ta => ta.Course);
        AddInclude(ta => ta.Class);
        ApplyOrderBy(ta => ta.Teacher.FullName);
        ApplyPaging(page, pageSize);

        var searchLower = search?.Trim().ToLowerInvariant();

        // ToLower() (not ToLowerInvariant()) below: this Criteria is an expression tree that EF
        // Core translates to SQL LOWER(...), which ToLowerInvariant() cannot be translated to.
        // The column value never touches client culture, so the CA1304/CA1311 concern doesn't apply.
#pragma warning disable CA1304, CA1311
        Criteria = ta =>
            (!teacherId.HasValue || ta.TeacherId == teacherId.Value) &&
            (!courseId.HasValue || ta.CourseId == courseId.Value) &&
            (!classId.HasValue || ta.ClassId == classId.Value) &&
            (string.IsNullOrWhiteSpace(searchLower) ||
             ta.Teacher.FullName.ToLower().Contains(searchLower) ||
             ta.Course.Name.ToLower().Contains(searchLower) ||
             ta.Class.Name.ToLower().Contains(searchLower));
#pragma warning restore CA1304, CA1311
    }
}
