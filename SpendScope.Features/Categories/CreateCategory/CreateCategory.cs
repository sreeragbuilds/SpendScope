using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SpendScope.Application.Abstractions;
using SpendScope.Application.Common;
using SpendScope.Contracts.Categories;
using SpendScope.Domain.Categories;
using SpendScope.Domain.Common;
using SpendScope.Domain.ValueObjects;
using SpendScope.Features.Common;

namespace SpendScope.Features.Categories.CreateCategory;

/// <summary>Spec 15.15. Name must not match any category the caller can see. FR-CAT-02 and 03.</summary>
public sealed class CreateCategoryValidator : AbstractValidator<CreateCategoryRequest>
{
    public CreateCategoryValidator()
    {
        RuleFor(x => x.Name).ValidCategoryName();
        RuleFor(x => x.ColorHex).ValidColorHex();
    }
}

public sealed class CreateCategoryHandler(IAppDbContext db, ICurrentUser caller, IClock clock)
{
    public async Task<Result<CategoryDto>> HandleAsync(CreateCategoryRequest request, CancellationToken ct)
    {
        var normalized = CategoryRules.Normalize(request.Name);

        // Visible = system categories + the caller's own (global filter).
        if (await db.Categories.AnyAsync(c => c.NormalizedName == normalized, ct))
        {
            return CategoryErrors.NameTaken;
        }

        var category = Category.CreateForUser(caller.UserId, request.Name, new HexColor(request.ColorHex), clock.UtcNow);
        db.Categories.Add(category);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Lost a race with a concurrent create of the same name: the unique index caught it.
            return CategoryErrors.NameTaken;
        }

        return new CategoryDto(category.Id, category.Name, category.ColorHex, false, 0);
    }
}

public sealed class CreateCategoryEndpoint : IFeatureEndpoint
{
    public void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/categories", async (CreateCategoryRequest request, CreateCategoryHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(request, ct)).ToHttpResult(dto => Results.Created($"/api/categories/{dto.Id}", dto)))
            .WithValidation<CreateCategoryRequest>()
            .RequireAuthorization(Policies.RequireUser)
            .WithTags("Categories")
            .WithName("CreateCategory")
            .Produces<CategoryDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict);
}
