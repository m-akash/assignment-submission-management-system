namespace AssignmentSystem.Shared.Common;

/// <summary>
/// Pagination bounds for every list endpoint. A client-supplied page size is never
/// trusted: without a ceiling, <c>?pageSize=1000000</c> is an unbounded query.
/// Applied where paging is turned into SQL, so no query object can bypass it.
/// </summary>
public static class PageDefaults
{
    public const int FirstPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public static int NormalizePage(int page) => page < FirstPage ? FirstPage : page;

    /// <summary>Nonsense sizes fall back to the default; oversized ones are capped, not rejected.</summary>
    public static int NormalizePageSize(int pageSize) => pageSize switch
    {
        < 1 => DefaultPageSize,
        > MaxPageSize => MaxPageSize,
        _ => pageSize,
    };
}
