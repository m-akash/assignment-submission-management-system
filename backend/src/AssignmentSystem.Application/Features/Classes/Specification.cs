using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.Classes;

namespace AssignmentSystem.Application.Features.Classes;

internal sealed class ClassesPagedSpecification : Specification<Class>
{
    /// <summary>Columns this endpoint may be sorted by. See <see cref="SortMap{T}"/>.</summary>
    private static readonly SortMap<Class> Sortable = new(
        new Dictionary<string, System.Linq.Expressions.Expression<Func<Class, object>>>
        {
            ["name"] = c => c.Name,
            ["level"] = c => c.Level,
            ["section"] = c => c.Section!,
            ["createdAt"] = c => c.CreatedAtUtc,
        },
        tieBreaker: c => c.Id);

    public ClassesPagedSpecification(string? search, string? sortBy, string? sortDir, int page, int pageSize)
    {
        ApplyNoTracking();
        if (!ApplySort(Sortable, sortBy, sortDir))
        {
            // By level then section, not by name: names carry Roman numerals, so sorting them
            // as text puts "Class IX" before "Class VI".
            ApplyOrderBy(c => c.Level);
            ApplyThenBy(c => c.Section!);
        }
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
