namespace AssignmentSystem.Shared.Common;

/// <summary>
/// Base query parameters for list endpoints: pagination, sorting, free-text search.
/// Filtering fields are added by feature-specific derived query objects.
/// </summary>
public abstract class PagedQuery
{
    private const int MaxPageSize = 100;

    private int _page = 1;
    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    private int _pageSize = 20;
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value is < 1 or > MaxPageSize ? 20 : value;
    }

    /// <summary>Free-text search term.</summary>
    public string? Search { get; set; }

    /// <summary>e.g. "deadlineUtc:desc" or "title". Default per feature.</summary>
    public string? Sort { get; set; }
}
