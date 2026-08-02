using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.Assignments;
using AssignmentSystem.Domain.Enums;

namespace AssignmentSystem.Application.Features.Assignments;

internal sealed class AssignmentWithDetailsSpecification : Specification<Assignment>
{
    public AssignmentWithDetailsSpecification(Guid id)
    {
        Criteria = a => a.Id == id;
        AddInclude(a => a.Subject);
        AddInclude(a => a.Class);
        AddInclude("TeacherAssignment.Teacher");
    }
}

internal sealed class AssignmentsPagedSpecification : Specification<Assignment>
{
    public AssignmentsPagedSpecification(
        Guid? classId,
        Guid? subjectId,
        Guid? teacherId,
        AssignmentStatus? status,
        string? search,
        int page,
        int pageSize)
    {
        ApplyNoTracking();
        AddInclude(a => a.Subject);
        AddInclude(a => a.Class);
        AddInclude("TeacherAssignment.Teacher");
        ApplyOrderByDescending(a => a.CreatedAtUtc);
        ApplyPaging(page, pageSize);

        var searchLower = search?.Trim().ToLowerInvariant();

        Criteria = a =>
            (!classId.HasValue || a.ClassId == classId.Value) &&
            (!subjectId.HasValue || a.SubjectId == subjectId.Value) &&
            (!teacherId.HasValue || a.TeacherId == teacherId.Value) &&
            (!status.HasValue || a.Status == status.Value) &&
            (string.IsNullOrWhiteSpace(searchLower) ||
             a.Title.ToLowerInvariant().Contains(searchLower) ||
             a.Description.ToLowerInvariant().Contains(searchLower));
    }
}
