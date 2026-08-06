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
    /// <summary>Columns this endpoint may be sorted by. See <see cref="SortMap{T}"/>.</summary>
    private static readonly SortMap<Submission> Sortable = new(
        new Dictionary<string, System.Linq.Expressions.Expression<Func<Submission, object>>>
        {
            ["student"] = s => s.Student.FullName,
            ["status"] = s => s.Status,
            ["marks"] = s => s.Marks!,
            ["submittedAt"] = s => s.SubmittedAtUtc!,
            ["createdAt"] = s => s.CreatedAtUtc,
        },
        tieBreaker: s => s.Id);

    public SubmissionsPagedSpecification(
        Guid? assignmentId,
        List<Guid>? assignmentIds,
        Guid? studentId,
        SubmissionStatus? status,
        string? search,
        string? sortBy,
        string? sortDir,
        int page,
        int pageSize)
    {
        ApplyNoTracking();
        AddInclude(s => s.Assignment);
        AddInclude(s => s.Student);
        AddInclude(s => s.ReviewedBy!);
        AddInclude(s => s.Files);
        if (!ApplySort(Sortable, sortBy, sortDir))
        {
            ApplyOrderByDescending(s => s.SubmittedAtUtc ?? s.CreatedAtUtc);
        }
        ApplyPaging(page, pageSize);

        var searchTerm = string.IsNullOrWhiteSpace(search) ? null : search.Trim().ToLowerInvariant();

        // ToLower() (not ToLowerInvariant()) below: this Criteria is an expression tree that EF
        // Core translates to SQL LOWER(...), which ToLowerInvariant() cannot be translated to.
        // The column value never touches client culture, so the CA1304/CA1311 concern doesn't apply.
#pragma warning disable CA1304, CA1311
        Criteria = s =>
            (!assignmentId.HasValue || s.AssignmentId == assignmentId.Value) &&
            (assignmentIds == null || assignmentIds.Contains(s.AssignmentId)) &&
            (!studentId.HasValue || s.StudentId == studentId.Value) &&
            (!status.HasValue || s.Status == status.Value) &&
            (searchTerm == null ||
             s.Student.FullName.ToLower().Contains(searchTerm) ||
             s.Assignment.Title.ToLower().Contains(searchTerm));
#pragma warning restore CA1304, CA1311
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
