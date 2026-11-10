using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SpendScope.Application.Abstractions;
using SpendScope.Contracts.Categories;
using SpendScope.Features.Common;

namespace SpendScope.Features.Categories.ListCategories;

/// <summary>Spec 15.14: system categories first, then the caller's own, by name.</summary>
public sealed class ListCategoriesHandler(IAppDbContext db)
{
    public Task<List<CategoryDto>> HandleAsync(CancellationToken ct) =>
        // Both sets are filtered to the caller, so ExpenseCount only counts the caller's expenses.
        db.Categories.AsNoTracking()
            .OrderByDescending(c => c.IsSystem)
            .ThenBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.ColorHex, c.IsSystem, db.Expenses.Count(e => e.CategoryId == c.Id)))
            .ToListAsync(ct);
}

public sealed class ListCategoriesEndpoint : IFeatureEndpoint
{
    public void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/categories", async (ListCategoriesHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.HandleAsync(ct)))
            .RequireAuthorization(Policies.RequireUser)
            .WithTags("Categories")
            .WithName("ListCategories")
            .Produces<List<CategoryDto>>();
}
