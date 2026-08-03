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

        // ToLower() (not ToLowerInvariant()) below: this Criteria is an expression tree that EF
        // Core translates to SQL LOWER(...), which ToLowerInvariant() cannot be translated to.
        // The column value never touches client culture, so the CA1304/CA1311 concern doesn't apply.
#pragma warning disable CA1304, CA1311
        Criteria = s =>
            string.IsNullOrWhiteSpace(searchLower) ||
            s.Name.ToLower().Contains(searchLower) ||
            s.Code.ToLower().Contains(searchLower);
#pragma warning restore CA1304, CA1311
    }
}
