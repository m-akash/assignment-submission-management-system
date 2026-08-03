using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.Departments;

namespace AssignmentSystem.Application.Features.Departments;

internal sealed class DepartmentByCodeSpecification : Specification<Department>
{
    public DepartmentByCodeSpecification(string code)
    {
        var normalized = code.Trim().ToUpperInvariant();
        Criteria = d => d.Code == normalized;
    }
}

internal sealed class DepartmentsPagedSpecification : Specification<Department>
{
    public DepartmentsPagedSpecification(string? search, int page, int pageSize)
    {
        ApplyNoTracking();
        ApplyOrderBy(d => d.Name);
        ApplyPaging(page, pageSize);

        var searchLower = search?.Trim().ToLowerInvariant();

        // ToLower() (not ToLowerInvariant()) below: this Criteria is an expression tree that EF
        // Core translates to SQL LOWER(...), which ToLowerInvariant() cannot be translated to.
        // The column value never touches client culture, so the CA1304/CA1311 concern doesn't apply.
#pragma warning disable CA1304, CA1311
        Criteria = d =>
            string.IsNullOrWhiteSpace(searchLower) ||
            d.Name.ToLower().Contains(searchLower) ||
            d.Code.ToLower().Contains(searchLower);
#pragma warning restore CA1304, CA1311
    }
}
