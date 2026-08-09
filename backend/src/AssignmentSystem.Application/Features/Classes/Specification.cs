using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.Classes;

namespace AssignmentSystem.Application.Features.Classes;

/// <summary>
/// The cohort occupying a (grade, section) slot, if any. Sections are compared
/// case-insensitively so "9-a" cannot slip past an existing "9-A".
/// </summary>
internal sealed class ClassByGradeAndSectionSpecification : Specification<Class>
{
    public ClassByGradeAndSectionSpecification(int level, string section)
    {
        var sectionLower = section.Trim().ToLowerInvariant();

        // ToLower() (not ToLowerInvariant()) below: this Criteria is an expression tree that EF
        // Core translates to SQL LOWER(...), which ToLowerInvariant() cannot be translated to.
#pragma warning disable CA1304, CA1311
        Criteria = c => c.Level == level && c.Section != null && c.Section.ToLower() == sectionLower;
#pragma warning restore CA1304, CA1311
    }
}

internal sealed class ClassesPagedSpecification : Specification<Class>
{
    /// <summary>Columns this endpoint may be sorted by. See <see cref="SortMap{T}"/>.</summary>
    private static readonly SortMap<Class> Sortable = new(
        new Dictionary<string, System.Linq.Expressions.Expression<Func<Class, object>>>
        {
            ["grade"] = c => c.Level,
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
            // Grade first, then section — the two columns the list shows, in the order it
            // shows them. Numeric on the grade, so 9 sorts before 10.
            ApplyOrderBy(c => c.Level);
            ApplyThenBy(c => c.Section!);
        }
        ApplyPaging(page, pageSize);

        var searchLower = search?.Trim().ToLowerInvariant();
        // "9" should find grade 9, and "a" should find section A. Parsed once here rather
        // than cast in SQL: a non-numeric term simply leaves the grade arm switched off.
        var searchLevel = int.TryParse(searchLower, out var parsed) ? parsed : (int?)null;

        // ToLower() (not ToLowerInvariant()) below: this Criteria is an expression tree that EF
        // Core translates to SQL LOWER(...), which ToLowerInvariant() cannot be translated to.
        // The column value never touches client culture, so the CA1304/CA1311 concern doesn't apply.
#pragma warning disable CA1304, CA1311
        Criteria = c =>
            string.IsNullOrWhiteSpace(searchLower) ||
            (searchLevel != null && c.Level == searchLevel) ||
            (c.Section != null && c.Section.ToLower().Contains(searchLower));
#pragma warning restore CA1304, CA1311
    }
}
