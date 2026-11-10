using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SpendScope.Application.Abstractions;
using SpendScope.Contracts.Reports;
using SpendScope.Domain.Common;
using SpendScope.Features.Common;

namespace SpendScope.Features.Reports.GetMonthlyTotals;

public sealed record MonthlyQuery(int? Year);

/// <summary>Spec 16.2. Always 12 entries. FR-RPT-03.</summary>
public sealed class MonthlyQueryValidator : AbstractValidator<MonthlyQuery>
{
    public const int MinYear = 2000;

    public MonthlyQueryValidator(IClock clock) =>
        RuleFor(x => x.Year)
            .Must(y => y is null || (y >= MinYear && y <= clock.Today.Year))
            .WithErrorCode("year_invalid")
            .WithMessage($"Year must be between {MinYear} and the current year.");
}

public sealed class GetMonthlyTotalsHandler(IAppDbContext db, IUserAccounts accounts, ICurrentUser caller, IClock clock)
{
    public async Task<MonthlyTotalsDto> HandleAsync(MonthlyQuery query, CancellationToken ct)
    {
        var year = query.Year ?? clock.Today.Year;
        var start = new DateOnly(year, 1, 1);
        var end = start.AddYears(1);

        // A range on OccurredOn (not .Year ==) so IX_Expenses_UserId_OccurredOn can be used.
        var rows = await db.Expenses.AsNoTracking()
            .Where(e => e.OccurredOn >= start && e.OccurredOn < end)
            .GroupBy(e => e.OccurredOn.Month)
            .Select(g => new { Month = g.Key, Total = g.Sum(e => e.Amount), Count = g.Count() })
            .ToListAsync(ct);

        var currency = await accounts.GetDefaultCurrencyAsync(caller.UserId, ct) ?? "USD";
        return new MonthlyTotalsDto(year, currency, ZeroFill(rows.ToDictionary(r => r.Month, r => (r.Total, r.Count))));
    }

    public static List<MonthTotalDto> ZeroFill(IReadOnlyDictionary<int, (decimal Total, int Count)> byMonth) =>
        Enumerable.Range(1, 12)
            .Select(m => byMonth.TryGetValue(m, out var v) ? new MonthTotalDto(m, v.Total, v.Count) : new MonthTotalDto(m, 0, 0))
            .ToList();
}

public sealed class GetMonthlyTotalsEndpoint : IFeatureEndpoint
{
    public void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/reports/monthly", async ([AsParameters] MonthlyQuery query, GetMonthlyTotalsHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.HandleAsync(query, ct)))
            .WithValidation<MonthlyQuery>()
            .RequireAuthorization(Policies.RequireUser)
            .WithTags("Reports")
            .WithName("GetMonthlyTotals")
            .Produces<MonthlyTotalsDto>();
}
