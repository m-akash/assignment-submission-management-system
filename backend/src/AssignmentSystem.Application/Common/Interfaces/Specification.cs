using System.Linq.Expressions;
using AssignmentSystem.Shared.Common;

namespace AssignmentSystem.Application.Common.Interfaces;

/// <summary>
/// Base specification implementation. Feature queries derive from this to build
/// reusable, composable, fully unit-testable query descriptions.
/// </summary>
public abstract class Specification<T> : ISpecification<T> where T : class
{
    public Expression<Func<T, bool>>? Criteria { get; protected set; }
    public List<Expression<Func<T, object>>> Includes { get; } = [];
    public List<string> IncludeStrings { get; } = [];
    public Expression<Func<T, object>>? OrderBy { get; protected set; }
    public Expression<Func<T, object>>? OrderByDescending { get; protected set; }
    public Expression<Func<T, object>>? ThenBy { get; protected set; }
    public int? Page { get; protected set; }
    public int? PageSize { get; protected set; }
    public bool AsNoTracking { get; protected set; }

    protected void AddInclude(Expression<Func<T, object>> include) => Includes.Add(include);
    protected void AddInclude(string includeString) => IncludeStrings.Add(includeString);
    /// <summary>
    /// Sets paging, normalising both values (see <see cref="PageDefaults"/>). This is the
    /// single choke point where a page size becomes Skip/Take, so clamping here means no
    /// query object can leak an unbounded request through — and the page size reported
    /// back in <c>PageResult</c> is the one actually used.
    /// </summary>
    protected void ApplyPaging(int page, int pageSize)
    {
        Page = PageDefaults.NormalizePage(page);
        PageSize = PageDefaults.NormalizePageSize(pageSize);
    }
    /// <summary>
    /// Applies a caller-requested sort if the key is one this endpoint allows, and reports
    /// whether it did. Callers order themselves when it returns false:
    ///
    /// <code>
    /// if (!ApplySort(Sortable, sortBy, sortDir))
    /// {
    ///     ApplyOrderBy(c => c.Level);
    ///     ApplyThenBy(c => c.Section!);
    /// }
    /// </code>
    ///
    /// That shape keeps each endpoint's natural order — which is usually a composite that no
    /// single sort key expresses — expressed where it is obvious, rather than encoded as a
    /// magic entry in the map.
    /// </summary>
    protected bool ApplySort(SortMap<T> sortable, string? sortBy, string? sortDir)
    {
        if (!sortable.TryResolve(sortBy, out var field))
        {
            return false;
        }

        if (SortDirection.IsDescending(sortDir))
        {
            ApplyOrderByDescending(field);
        }
        else
        {
            ApplyOrderBy(field);
        }

        // See SortMap.TieBreaker: without this, paging over a non-unique sort column can
        // show the same row twice or skip it entirely.
        ApplyThenBy(sortable.TieBreaker);
        return true;
    }

    protected void ApplyOrderBy(Expression<Func<T, object>> orderBy) => OrderBy = orderBy;
    protected void ApplyOrderByDescending(Expression<Func<T, object>> orderByDescending) => OrderByDescending = orderByDescending;
    protected void ApplyThenBy(Expression<Func<T, object>> thenBy) => ThenBy = thenBy;
    protected void ApplyNoTracking() => AsNoTracking = true;
}
