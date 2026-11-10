using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SpendScope.Application.Abstractions;
using SpendScope.Application.Common;
using SpendScope.Features.Common;

namespace SpendScope.Features.Expenses.DeleteExpense;

/// <summary>Spec 15.10. Hard delete. FR-EXP-09 and 10.</summary>
public sealed class DeleteExpenseHandler(IAppDbContext db, ICurrentUser caller)
{
    public async Task<Result> HandleAsync(Guid id, CancellationToken ct)
    {
        var expense = await db.Expenses.SingleOrDefaultAsync(e => e.Id == id && e.UserId == caller.UserId, ct);
        if (expense is null)
        {
            return Error.NotFound("expense_not_found", "Expense not found.");
        }

        db.Expenses.Remove(expense);
        await db.SaveChangesAsync(ct);
        return Result.Success;
    }
}

public sealed class DeleteExpenseEndpoint : IFeatureEndpoint
{
    public void Map(IEndpointRouteBuilder app) =>
        app.MapDelete("/api/expenses/{id:guid}", async (Guid id, DeleteExpenseHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(id, ct)).ToHttpResult(Results.NoContent))
            .RequireAuthorization(Policies.RequireUser)
            .WithTags("Expenses")
            .WithName("DeleteExpense")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
}
