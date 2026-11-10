using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SpendScope.Application.Abstractions;
using SpendScope.Application.Common;
using SpendScope.Contracts.Categories;
using SpendScope.Domain.Categories;
using SpendScope.Domain.ValueObjects;
using SpendScope.Features.Common;

namespace SpendScope.Features.Categories.UpdateCategory;

/// <summary>Spec 15.16. System → 403; another user's → 404. FR-CAT-04 and 06.</summary>
public sealed class UpdateCategoryValidator : AbstractValidator<UpdateCategoryRequest>
{
    public UpdateCategoryValidator()
    {
        RuleFor(x => x.Name).ValidCategoryName();
        RuleFor(x => x.ColorHex).ValidColorHex();
    }
}

public sealed class UpdateCategoryHandler(IAppDbContext db, ICurrentUser caller)
{
    public async Task<Result<CategoryDto>> HandleAsync(Guid id, UpdateCategoryRequest request, CancellationToken ct)
    {
        var category = await db.Categories.SingleOrDefaultAsync(c => c.Id == id, ct);
        if (category is null)
        {
            return CategoryErrors.NotFound;
        }

        if (category.IsSystem)
        {
            return CategoryErrors.ReadOnly;
        }

        if (!category.IsOwnedBy(caller.UserId))
        {
            return CategoryErrors.NotFound;
        }

        var normalized = CategoryRules.Normalize(request.Name);
        if (await db.Categories.AnyAsync(c => c.Id != id && c.NormalizedName == normalized, ct))
        {
            return CategoryErrors.NameTaken;
        }

        category.Rename(request.Name);
        category.Recolor(new HexColor(request.ColorHex));

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return CategoryErrors.NameTaken;
        }

        var count = await db.Expenses.CountAsync(e => e.CategoryId == id, ct);
        return new CategoryDto(category.Id, category.Name, category.ColorHex, false, count);
    }
}

public sealed class UpdateCategoryEndpoint : IFeatureEndpoint
{
    public void Map(IEndpointRouteBuilder app) =>
        app.MapPut("/api/categories/{id:guid}", async (Guid id, UpdateCategoryRequest request, UpdateCategoryHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(id, request, ct)).ToHttpResult(Results.Ok))
            .WithValidation<UpdateCategoryRequest>()
            .RequireAuthorization(Policies.RequireUser)
            .WithTags("Categories")
            .WithName("UpdateCategory")
            .Produces<CategoryDto>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
}
