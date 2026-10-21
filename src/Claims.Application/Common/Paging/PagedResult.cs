namespace Claims.Application.Common.Paging;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public static class Paging
{
    public const int MaxPageSize = 100;
    public const int DefaultPageSize = 20;
}
