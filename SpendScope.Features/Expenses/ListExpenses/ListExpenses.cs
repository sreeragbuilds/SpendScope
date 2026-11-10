using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SpendScope.Application.Abstractions;
using SpendScope.Application.Common;
using SpendScope.Contracts.Common;
using SpendScope.Contracts.Expenses;
using SpendScope.Domain.Common;
using SpendScope.Features.Common;

namespace SpendScope.Features.Expenses.ListExpenses;

/// <summary>Spec 15.12. FR-EXP-04 to 06.</summary>
public sealed class ListExpensesHandler(IAppDbContext db, IClock clock)
{
    public async Task<Result<PagedResponse<ExpenseDto>>> HandleAsync(ExpenseQuery query, CancellationToken ct)
    {
        if (query.CategoryId is { } categoryId && !await db.Categories.AnyAsync(c => c.Id == categoryId, ct))
        {
            return Error.Validation("category_not_found", "Category does not exist.", "categoryId");
        }

        var page = query.Page ?? 1;
        var pageSize = query.PageSize ?? Pagination.DefaultPageSize;
        var range = DateRange.Resolve(query.From, query.To, clock.Today);

        // The global filter already restricts db.Expenses to the caller.
        var filtered = db.Expenses.AsNoTracking().ApplyFilters(query, range);
        var total = await filtered.CountAsync(ct);
        var items = await filtered
            .ApplySort(query)
            .Skip(Pagination.Skip(page, pageSize))
            .Take(pageSize)
            .Select(ExpenseQueries.ToDto)
            .ToListAsync(ct);

        return new PagedResponse<ExpenseDto>(items, page, pageSize, total);
    }
}

public sealed class ListExpensesEndpoint : IFeatureEndpoint
{
    public void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/expenses", async ([AsParameters] ExpenseQuery query, ListExpensesHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(query, ct)).ToHttpResult(Results.Ok))
            .WithValidation<ExpenseQuery>()
            .RequireAuthorization(Policies.RequireUser)
            .WithTags("Expenses")
            .WithName("ListExpenses")
            .Produces<PagedResponse<ExpenseDto>>();
}
