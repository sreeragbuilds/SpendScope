using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SpendScope.Application.Abstractions;
using SpendScope.Contracts.Reports;
using SpendScope.Domain.Common;
using SpendScope.Features.Common;

namespace SpendScope.Features.Reports.GetCategorySummary;

public sealed record SummaryQuery(DateOnly? From, DateOnly? To);

/// <summary>Spec 16.1 and 16.3. FR-RPT-01, 02, 04 and 06.</summary>
public sealed class SummaryQueryValidator : AbstractValidator<SummaryQuery>
{
    public SummaryQueryValidator(IClock clock) =>
        this.ValidDateRange(q => DateRange.Resolve(q.From, q.To, clock.Today));
}

public sealed class GetCategorySummaryHandler(IAppDbContext db, IUserAccounts accounts, ICurrentUser caller, IClock clock)
{
    public async Task<CategorySummaryDto> HandleAsync(SummaryQuery query, CancellationToken ct)
    {
        var range = DateRange.Resolve(query.From, query.To, clock.Today);

        // Aggregated in SQL; the per-row list returned here is at most one row per category.
        var rows = await db.Expenses.AsNoTracking()
            .Where(e => e.OccurredOn >= range.From && e.OccurredOn <= range.To)
            .GroupBy(e => new { e.CategoryId, e.Category!.Name, e.Category.ColorHex })
            .Select(g => new { g.Key.CategoryId, g.Key.Name, g.Key.ColorHex, Total = g.Sum(e => e.Amount), Count = g.Count() })
            .OrderByDescending(x => x.Total)
            .ToListAsync(ct);

        var grandTotal = rows.Sum(r => r.Total);
        var count = rows.Sum(r => r.Count);
        var currency = await accounts.GetDefaultCurrencyAsync(caller.UserId, ct) ?? "USD";

        var categories = rows
            .Select(r => new CategoryTotalDto(r.CategoryId, r.Name, r.ColorHex, r.Total, r.Count, ReportMath.Percent(r.Total, grandTotal)))
            .ToList();

        return new CategorySummaryDto(
            range.From,
            range.To,
            currency,
            grandTotal,
            count,
            ReportMath.Average(grandTotal, count),
            categories);
    }
}

public sealed class GetCategorySummaryEndpoint : IFeatureEndpoint
{
    public void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/reports/summary", async ([AsParameters] SummaryQuery query, GetCategorySummaryHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.HandleAsync(query, ct)))
            .WithValidation<SummaryQuery>()
            .RequireAuthorization(Policies.RequireUser)
            .WithTags("Reports")
            .WithName("GetCategorySummary")
            .Produces<CategorySummaryDto>();
}
