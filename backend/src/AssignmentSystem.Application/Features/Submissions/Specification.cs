using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.Submissions;
using AssignmentSystem.Domain.Enums;

namespace AssignmentSystem.Application.Features.Submissions;

internal sealed class SubmissionWithDetailsSpecification : Specification<Submission>
{
    public SubmissionWithDetailsSpecification(Guid id)
    {
        Criteria = s => s.Id == id;
        AddInclude(s => s.Assignment);
        AddInclude(s => s.Student);
        AddInclude(s => s.ReviewedBy!);
        AddInclude(s => s.Files);
    }
}

internal sealed class SubmissionByStudentAndAssignmentSpecification : Specification<Submission>
{
    public SubmissionByStudentAndAssignmentSpecification(Guid studentId, Guid assignmentId)
    {
        Criteria = s => s.StudentId == studentId && s.AssignmentId == assignmentId;
        AddInclude(s => s.Assignment);
        AddInclude(s => s.Student);
        AddInclude(s => s.ReviewedBy!);
        AddInclude(s => s.Files);
    }
}

internal sealed class SubmissionsPagedSpecification : Specification<Submission>
{
    public SubmissionsPagedSpecification(
        Guid? assignmentId,
        List<Guid>? assignmentIds,
        Guid? studentId,
        SubmissionStatus? status,
        int page,
        int pageSize)
    {
        ApplyNoTracking();
        AddInclude(s => s.Assignment);
        AddInclude(s => s.Student);
        AddInclude(s => s.ReviewedBy!);
        AddInclude(s => s.Files);
        ApplyOrderByDescending(s => s.SubmittedAtUtc ?? s.CreatedAtUtc);
        ApplyPaging(page, pageSize);

        Criteria = s =>
            (!assignmentId.HasValue || s.AssignmentId == assignmentId.Value) &&
            (assignmentIds == null || assignmentIds.Contains(s.AssignmentId)) &&
            (!studentId.HasValue || s.StudentId == studentId.Value) &&
            (!status.HasValue || s.Status == status.Value);
    }
}

internal sealed class SubmissionFileByIdSpecification : Specification<SubmissionFile>
{
    public SubmissionFileByIdSpecification(Guid fileId)
    {
        Criteria = sf => sf.Id == fileId;
        AddInclude(sf => sf.Submission);
    }
}
