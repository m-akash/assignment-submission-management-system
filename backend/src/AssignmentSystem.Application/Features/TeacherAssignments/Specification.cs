using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.TeacherAssignments;

namespace AssignmentSystem.Application.Features.TeacherAssignments;

internal sealed class TeacherAssignmentWithDetailsSpecification : Specification<TeacherAssignment>
{
    public TeacherAssignmentWithDetailsSpecification(Guid id)
    {
        Criteria = ta => ta.Id == id;
        AddInclude(ta => ta.Teacher);
        AddInclude(ta => ta.Subject);
        AddInclude(ta => ta.Class);
    }
}

internal sealed class TeacherAssignmentDuplicateSpecification : Specification<TeacherAssignment>
{
    public TeacherAssignmentDuplicateSpecification(Guid teacherId, Guid subjectId, Guid classId)
    {
        Criteria = ta => ta.TeacherId == teacherId && ta.SubjectId == subjectId && ta.ClassId == classId;
    }
}

internal sealed class TeacherAssignmentsPagedSpecification : Specification<TeacherAssignment>
{
    public TeacherAssignmentsPagedSpecification(
        Guid? teacherId, Guid? subjectId, Guid? classId, string? search, int page, int pageSize)
    {
        ApplyNoTracking();
        AddInclude(ta => ta.Teacher);
        AddInclude(ta => ta.Subject);
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
            (!subjectId.HasValue || ta.SubjectId == subjectId.Value) &&
            (!classId.HasValue || ta.ClassId == classId.Value) &&
            (string.IsNullOrWhiteSpace(searchLower) ||
             ta.Teacher.FullName.ToLower().Contains(searchLower) ||
             ta.Subject.Name.ToLower().Contains(searchLower) ||
             ta.Class.Name.ToLower().Contains(searchLower));
#pragma warning restore CA1304, CA1311
    }
}
