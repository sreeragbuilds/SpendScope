using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SpendScope.Application.Abstractions;
using SpendScope.Application.Common;
using SpendScope.Contracts.Expenses;
using SpendScope.Features.Common;

namespace SpendScope.Features.Expenses.GetExpense;

/// <summary>Spec 15.11. FR-EXP-07 and 10.</summary>
public sealed class GetExpenseHandler(IAppDbContext db, ICurrentUser caller)
{
    public async Task<Result<ExpenseDto>> HandleAsync(Guid id, CancellationToken ct)
    {
        var dto = await db.Expenses.AsNoTracking()
            .Where(e => e.Id == id && e.UserId == caller.UserId)
            .Select(ExpenseQueries.ToDto)
            .SingleOrDefaultAsync(ct);

        return dto is null ? Error.NotFound("expense_not_found", "Expense not found.") : dto;
    }
}

public sealed class GetExpenseEndpoint : IFeatureEndpoint
{
    public void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/expenses/{id:guid}", async (Guid id, GetExpenseHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(id, ct)).ToHttpResult(Results.Ok))
            .RequireAuthorization(Policies.RequireUser)
            .WithTags("Expenses")
            .WithName("GetExpense")
            .Produces<ExpenseDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);
}
