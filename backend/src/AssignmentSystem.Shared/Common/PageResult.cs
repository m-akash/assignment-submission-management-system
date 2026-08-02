namespace AssignmentSystem.Shared.Common;

/// <summary>
/// A page of results plus pagination metadata. Returned by list/paged queries and
/// surfaced in the API envelope's <c>pagination</c> field.
/// </summary>
public sealed class PageResult<T>
{
    public IReadOnlyList<T> Items { get; }
    public int Page { get; }
    public int PageSize { get; }
    public int Total { get; }
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);
    public bool HasNext => Page < TotalPages;
    public bool HasPrevious => Page > 1;

    public PageResult(IReadOnlyList<T> items, int page, int pageSize, int total)
    {
        Items = items;
        Page = page;
        PageSize = pageSize;
        Total = total;
    }

    public static PageResult<T> Empty(int page, int pageSize) => new(Array.Empty<T>(), page, pageSize, 0);
}
