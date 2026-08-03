using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.Classes;

namespace AssignmentSystem.Application.Features.Classes;

internal sealed class ClassesPagedSpecification : Specification<Class>
{
    public ClassesPagedSpecification(string? search, int page, int pageSize)
    {
        ApplyNoTracking();
        ApplyOrderBy(c => c.Name);
        ApplyPaging(page, pageSize);

        var searchLower = search?.Trim().ToLowerInvariant();

        // ToLower() (not ToLowerInvariant()) below: this Criteria is an expression tree that EF
        // Core translates to SQL LOWER(...), which ToLowerInvariant() cannot be translated to.
        // The column value never touches client culture, so the CA1304/CA1311 concern doesn't apply.
#pragma warning disable CA1304, CA1311
        Criteria = c =>
            string.IsNullOrWhiteSpace(searchLower) ||
            c.Name.ToLower().Contains(searchLower) ||
            (c.Grade != null && c.Grade.ToLower().Contains(searchLower)) ||
            (c.Section != null && c.Section.ToLower().Contains(searchLower));
#pragma warning restore CA1304, CA1311
    }
}
