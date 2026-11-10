using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SpendScope.Application.Abstractions;
using SpendScope.Application.Common;
using SpendScope.Contracts.Common;
using SpendScope.Features.Common;

namespace SpendScope.Features.Categories.DeleteCategory;

/// <summary>
/// Spec 15.17. If expenses use the category, a visible replacement is required and those
/// expenses move to it. Reassign and delete happen in one transaction. FR-CAT-05 and 06.
/// </summary>
public sealed class DeleteCategoryHandler(IAppDbContext db, ICurrentUser caller)
{
    public async Task<Result> HandleAsync(Guid id, Guid? replacementId, CancellationToken ct)
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

        var inUse = await db.Expenses.CountAsync(e => e.CategoryId == id, ct);
        if (inUse > 0 && replacementId is null)
        {
            return Error.Validation(
                ErrorCodes.ReplacementRequired,
                $"{inUse} expense(s) use this category. Choose a replacement category.",
                "replacementId");
        }

        if (replacementId is { } replacement)
        {
            if (replacement == id || !await db.Categories.AnyAsync(c => c.Id == replacement, ct))
            {
                return Error.Validation("replacement_invalid", "Choose a different, existing category.", "replacementId");
            }
        }

        await using var transaction = await db.BeginTransactionAsync(ct);

        if (inUse > 0)
        {
            // The Expenses filter limits this to the caller's own rows.
            await db.Expenses
                .Where(e => e.CategoryId == id)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.CategoryId, replacementId!.Value), ct);
        }

        db.Categories.Remove(category);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return Result.Success;
    }
}

public sealed class DeleteCategoryEndpoint : IFeatureEndpoint
{
    public void Map(IEndpointRouteBuilder app) =>
        app.MapDelete("/api/categories/{id:guid}", async (Guid id, Guid? replacementId, DeleteCategoryHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(id, replacementId, ct)).ToHttpResult(Results.NoContent))
            .RequireAuthorization(Policies.RequireUser)
            .WithTags("Categories")
            .WithName("DeleteCategory")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
}
