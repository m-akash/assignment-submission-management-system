using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.Assignments;
using AssignmentSystem.Domain.Enums;

namespace AssignmentSystem.Application.Features.Assignments;

internal sealed class AssignmentWithDetailsSpecification : Specification<Assignment>
{
    public AssignmentWithDetailsSpecification(Guid id)
    {
        Criteria = a => a.Id == id;
        AddInclude(a => a.Course);
        AddInclude(a => a.Class);
        AddInclude("TeacherAssignment.Teacher");
    }
}

internal sealed class AssignmentsPagedSpecification : Specification<Assignment>
{
    public AssignmentsPagedSpecification(
        Guid? classId,
        Guid? courseId,
        Guid? teacherId,
        AssignmentStatus? status,
        string? search,
        int page,
        int pageSize)
    {
        ApplyNoTracking();
        AddInclude(a => a.Course);
        AddInclude(a => a.Class);
        AddInclude("TeacherAssignment.Teacher");
        ApplyOrderByDescending(a => a.CreatedAtUtc);
        ApplyPaging(page, pageSize);

        var searchLower = search?.Trim().ToLowerInvariant();

        // ToLower() (not ToLowerInvariant()) below: this Criteria is an expression tree that EF
        // Core translates to SQL LOWER(...), which ToLowerInvariant() cannot be translated to.
        // The column value never touches client culture, so the CA1304/CA1311 concern doesn't apply.
#pragma warning disable CA1304, CA1311
        Criteria = a =>
            (!classId.HasValue || a.ClassId == classId.Value) &&
            (!courseId.HasValue || a.CourseId == courseId.Value) &&
            (!teacherId.HasValue || a.TeacherId == teacherId.Value) &&
            (!status.HasValue || a.Status == status.Value) &&
            (string.IsNullOrWhiteSpace(searchLower) ||
             a.Title.ToLower().Contains(searchLower) ||
             a.Description.ToLower().Contains(searchLower));
#pragma warning restore CA1304, CA1311
    }
}
