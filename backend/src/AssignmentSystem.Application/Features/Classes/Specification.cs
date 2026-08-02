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

        Criteria = c =>
            string.IsNullOrWhiteSpace(searchLower) ||
            c.Name.ToLowerInvariant().Contains(searchLower) ||
            (c.Grade != null && c.Grade.ToLowerInvariant().Contains(searchLower)) ||
            (c.Section != null && c.Section.ToLowerInvariant().Contains(searchLower));
    }
}
