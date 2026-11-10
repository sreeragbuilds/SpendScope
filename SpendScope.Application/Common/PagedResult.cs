namespace SpendScope.Application.Common;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

/// <summary>Paging limits from spec FR-EXP-04 and section 14.4.</summary>
public static class Pagination
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public static int Skip(int page, int pageSize) => (Math.Max(page, 1) - 1) * pageSize;
}
