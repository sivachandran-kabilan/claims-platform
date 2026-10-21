using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Claims.Application.Common.Paging;

public static class PagingExtensions
{
    /// <summary>Count + one page in SQL. The query must already be ordered.</summary>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query, int page, int pageSize, CancellationToken cancellationToken)
    {
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<T>(items, page, pageSize, total);
    }

    public static void ValidPaging<T>(this AbstractValidator<T> validator, Func<T, int> page, Func<T, int> pageSize)
    {
        validator.RuleFor(x => page(x)).GreaterThanOrEqualTo(1).OverridePropertyName("Page");
        validator.RuleFor(x => pageSize(x)).InclusiveBetween(1, Paging.MaxPageSize).OverridePropertyName("PageSize");
    }
}
