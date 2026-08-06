using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.Courses;

namespace AssignmentSystem.Application.Features.Courses;

internal sealed class CourseByCodeSpecification : Specification<Course>
{
    public CourseByCodeSpecification(string code)
    {
        var normalized = code.Trim().ToUpperInvariant();
        Criteria = s => s.Code == normalized;
    }
}

internal sealed class CoursesPagedSpecification : Specification<Course>
{
    /// <summary>Columns this endpoint may be sorted by. See <see cref="SortMap{T}"/>.</summary>
    private static readonly SortMap<Course> Sortable = new(
        new Dictionary<string, System.Linq.Expressions.Expression<Func<Course, object>>>
        {
            ["name"] = s => s.Name,
            ["code"] = s => s.Code,
            ["createdAt"] = s => s.CreatedAtUtc,
        },
        tieBreaker: s => s.Id);

    public CoursesPagedSpecification(string? search, string? sortBy, string? sortDir, int page, int pageSize)
    {
        ApplyNoTracking();
        if (!ApplySort(Sortable, sortBy, sortDir))
        {
            ApplyOrderBy(s => s.Name);
        }
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
