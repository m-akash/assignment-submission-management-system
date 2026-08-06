using System.Linq.Expressions;

namespace AssignmentSystem.Application.Common.Interfaces;

/// <summary>
/// The sortable columns of one list endpoint, as an allow-list.
///
/// A map rather than reflection over the entity: <c>sortBy</c> arrives from the query string,
/// and turning arbitrary caller-supplied text into a property access is how a list endpoint
/// starts ordering by a password hash, or throwing on a name that does not exist. Only the
/// keys named here can be sorted by, and anything else quietly falls back to the endpoint's
/// natural order.
///
/// The keys are the names the API contract exposes — deliberately not the C# property names,
/// so a rename inside the domain is not a breaking change to callers.
/// </summary>
public sealed class SortMap<T>
{
    private readonly Dictionary<string, Expression<Func<T, object>>> _fields;

    /// <param name="fields">Public sort key → the expression to order by.</param>
    /// <param name="tieBreaker">
    /// Applied after any explicit sort. Sorting by a non-unique column — a class level, a
    /// status — leaves rows with equal values in whatever order the database returns them,
    /// which can differ between two queries; a row can then appear on page 1 and page 2, or
    /// on neither. A unique tiebreaker makes the order total, and paging deterministic.
    /// </param>
    public SortMap(
        IReadOnlyDictionary<string, Expression<Func<T, object>>> fields,
        Expression<Func<T, object>> tieBreaker)
    {
        _fields = new Dictionary<string, Expression<Func<T, object>>>(fields, StringComparer.OrdinalIgnoreCase);
        TieBreaker = tieBreaker;
    }

    public Expression<Func<T, object>> TieBreaker { get; }

    /// <summary>The accepted sort keys, for documentation and error messages.</summary>
    public IReadOnlyCollection<string> Keys => _fields.Keys;

    public bool TryResolve(string? sortBy, out Expression<Func<T, object>> field)
    {
        field = null!;
        if (string.IsNullOrWhiteSpace(sortBy))
        {
            return false;
        }

        return _fields.TryGetValue(sortBy.Trim(), out field!);
    }
}

/// <summary>Sort direction as it arrives on the query string.</summary>
public static class SortDirection
{
    public const string Ascending = "asc";
    public const string Descending = "desc";

    /// <summary>
    /// Ascending unless descending is asked for explicitly. Anything unrecognised is treated
    /// as ascending rather than rejected: a typo in a sort direction is not worth failing a
    /// request that is otherwise perfectly well formed.
    /// </summary>
    public static bool IsDescending(string? sortDir) =>
        string.Equals(sortDir?.Trim(), Descending, StringComparison.OrdinalIgnoreCase);
}
