using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SpendScope.Application.Abstractions;
using SpendScope.Application.Common;
using SpendScope.Contracts.Expenses;
using SpendScope.Domain.Common;
using SpendScope.Domain.Expenses;
using SpendScope.Domain.ValueObjects;
using SpendScope.Features.Common;

namespace SpendScope.Features.Expenses.CreateExpense;

/// <summary>Spec 15.8 and appendix A.3. FR-EXP-01 to 03.</summary>
public sealed class CreateExpenseValidator : AbstractValidator<CreateExpenseRequest>
{
    public CreateExpenseValidator(IClock clock)
    {
        RuleFor(x => x.Amount).ValidAmount();
        RuleFor(x => x.OccurredOn).NotTooFarInFuture(clock);
        RuleFor(x => x.CategoryId).NotEmpty().WithErrorCode("category_required").WithMessage("Choose a category.");
        RuleFor(x => x.Note).ValidNote();
    }
}

public sealed class CreateExpenseHandler(IAppDbContext db, IUserAccounts accounts, ICurrentUser caller, IClock clock)
{
    public async Task<Result<ExpenseDto>> HandleAsync(CreateExpenseRequest request, CancellationToken ct)
    {
        // The Categories filter already limits this to system categories and the caller's own.
        if (!await db.Categories.AnyAsync(c => c.Id == request.CategoryId, ct))
        {
            return Error.Validation("category_not_found", "Category does not exist.", "categoryId");
        }

        var currency = await accounts.GetDefaultCurrencyAsync(caller.UserId, ct) ?? "USD";

        var expense = Expense.Create(
            caller.UserId,
            new Money(request.Amount, currency),
            request.OccurredOn,
            request.CategoryId,
            request.Note,
            clock.UtcNow);

        db.Expenses.Add(expense);
        await db.SaveChangesAsync(ct);

        return await db.Expenses.AsNoTracking().Where(e => e.Id == expense.Id).Select(ExpenseQueries.ToDto).SingleAsync(ct);
    }
}

public sealed class CreateExpenseEndpoint : IFeatureEndpoint
{
    public void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/expenses", async (CreateExpenseRequest request, CreateExpenseHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(request, ct)).ToHttpResult(dto => Results.Created($"/api/expenses/{dto.Id}", dto)))
            .WithValidation<CreateExpenseRequest>()
            .RequireAuthorization(Policies.RequireUser)
            .WithTags("Expenses")
            .WithName("CreateExpense")
            .WithSummary("Create an expense for the signed-in user.")
            .Produces<ExpenseDto>(StatusCodes.Status201Created);
}
