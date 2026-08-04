using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.Classes;

namespace AssignmentSystem.Application.Features.Classes;

internal sealed class ClassesPagedSpecification : Specification<Class>
{
    public ClassesPagedSpecification(string? search, int page, int pageSize)
    {
        ApplyNoTracking();
        // By level then section, not by name: names carry Roman numerals, so sorting them
        // as text puts "Class IX" before "Class VI".
        ApplyOrderBy(c => c.Level);
        ApplyThenBy(c => c.Section!);
        ApplyPaging(page, pageSize);

        var searchLower = search?.Trim().ToLowerInvariant();

        // The grade is a number now, so it is not searched directly — the name carries the
        // numeral ("Class IX - Section A"), which is what someone would type anyway.
        // ToLower() (not ToLowerInvariant()) below: this Criteria is an expression tree that EF
        // Core translates to SQL LOWER(...), which ToLowerInvariant() cannot be translated to.
        // The column value never touches client culture, so the CA1304/CA1311 concern doesn't apply.
#pragma warning disable CA1304, CA1311
        Criteria = c =>
            string.IsNullOrWhiteSpace(searchLower) ||
            c.Name.ToLower().Contains(searchLower) ||
            (c.Section != null && c.Section.ToLower().Contains(searchLower));
#pragma warning restore CA1304, CA1311
    }
}
