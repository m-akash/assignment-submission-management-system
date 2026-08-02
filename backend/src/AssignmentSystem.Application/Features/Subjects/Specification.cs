using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.Subjects;

namespace AssignmentSystem.Application.Features.Subjects;

internal sealed class SubjectByCodeSpecification : Specification<Subject>
{
    public SubjectByCodeSpecification(string code)
    {
        var normalized = code.Trim().ToUpperInvariant();
        Criteria = s => s.Code == normalized;
    }
}

internal sealed class SubjectsPagedSpecification : Specification<Subject>
{
    public SubjectsPagedSpecification(string? search, int page, int pageSize)
    {
        ApplyNoTracking();
        ApplyOrderBy(s => s.Name);
        ApplyPaging(page, pageSize);

        var searchLower = search?.Trim().ToLowerInvariant();

        Criteria = s =>
            string.IsNullOrWhiteSpace(searchLower) ||
            s.Name.ToLowerInvariant().Contains(searchLower) ||
            s.Code.ToLowerInvariant().Contains(searchLower);
    }
}
