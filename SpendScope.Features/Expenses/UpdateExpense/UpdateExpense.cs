using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SpendScope.Application.Abstractions;
using SpendScope.Application.Common;
using SpendScope.Contracts.Expenses;
using SpendScope.Domain.Common;
using SpendScope.Features.Common;

namespace SpendScope.Features.Expenses.UpdateExpense;

/// <summary>Spec 15.9. Another user's expense is 404, never 403. FR-EXP-08 and 10.</summary>
public sealed class UpdateExpenseValidator : AbstractValidator<UpdateExpenseRequest>
{
    public UpdateExpenseValidator(IClock clock)
    {
        RuleFor(x => x.Amount).ValidAmount();
        RuleFor(x => x.OccurredOn).NotTooFarInFuture(clock);
        RuleFor(x => x.CategoryId).NotEmpty().WithErrorCode("category_required").WithMessage("Choose a category.");
        RuleFor(x => x.Note).ValidNote();
    }
}

public sealed class UpdateExpenseHandler(IAppDbContext db, ICurrentUser caller, IClock clock)
{
    public async Task<Result<ExpenseDto>> HandleAsync(Guid id, UpdateExpenseRequest request, CancellationToken ct)
    {
        // Explicit owner check on top of the global filter (defense in depth, spec 13.9).
        var expense = await db.Expenses.SingleOrDefaultAsync(e => e.Id == id && e.UserId == caller.UserId, ct);
        if (expense is null)
        {
            return Error.NotFound("expense_not_found", "Expense not found.");
        }

        if (!await db.Categories.AnyAsync(c => c.Id == request.CategoryId, ct))
        {
            return Error.Validation("category_not_found", "Category does not exist.", "categoryId");
        }

        expense.Update(request.Amount, request.OccurredOn, request.CategoryId, request.Note, clock.UtcNow);
        await db.SaveChangesAsync(ct);

        return await db.Expenses.AsNoTracking().Where(e => e.Id == id).Select(ExpenseQueries.ToDto).SingleAsync(ct);
    }
}

public sealed class UpdateExpenseEndpoint : IFeatureEndpoint
{
    public void Map(IEndpointRouteBuilder app) =>
        app.MapPut("/api/expenses/{id:guid}", async (Guid id, UpdateExpenseRequest request, UpdateExpenseHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(id, request, ct)).ToHttpResult(Results.Ok))
            .WithValidation<UpdateExpenseRequest>()
            .RequireAuthorization(Policies.RequireUser)
            .WithTags("Expenses")
            .WithName("UpdateExpense")
            .Produces<ExpenseDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);
}
