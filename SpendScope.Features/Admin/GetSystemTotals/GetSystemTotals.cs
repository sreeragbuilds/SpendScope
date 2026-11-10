using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SpendScope.Application.Abstractions;
using SpendScope.Contracts.Admin;
using SpendScope.Domain.Categories;
using SpendScope.Domain.Common;
using SpendScope.Features.Common;

namespace SpendScope.Features.Admin.GetSystemTotals;

public sealed record SystemTotalsQuery(DateOnly? From, DateOnly? To);

public sealed record SystemMonthlyQuery(int? Year);

public sealed class SystemTotalsQueryValidator : AbstractValidator<SystemTotalsQuery>
{
    public SystemTotalsQueryValidator(IClock clock) =>
        this.ValidDateRange(q => DateRange.Resolve(q.From, q.To, clock.Today));
}

public sealed class SystemMonthlyQueryValidator : AbstractValidator<SystemMonthlyQuery>
{
    public SystemMonthlyQueryValidator(IClock clock) =>
        RuleFor(x => x.Year)
            .Must(y => y is null || (y >= 2000 && y <= clock.Today.Year))
            .WithErrorCode("year_invalid")
            .WithMessage("Year must be between 2000 and the current year.");
}

/// <summary>
/// Spec 15.22 and 16.5: aggregates across all users. This is the one reviewed place that
/// bypasses the per-user query filters. It returns totals only: never user IDs, never rows.
/// User-defined categories are combined into one bucket so their (private) names are not exposed.
/// FR-ADM-03 and 04.
/// </summary>
public sealed class GetSystemTotalsHandler(IAppDbContext db, IClock clock)
{
    public const string CustomBucketName = "Custom categories";
    public const string CustomBucketColor = "#94A3B8";

    public async Task<SystemCategoryTotalsDto> CategoryTotalsAsync(SystemTotalsQuery query, CancellationToken ct)
    {
        var range = DateRange.Resolve(query.From, query.To, clock.Today);

        var rows = await db.Expenses.IgnoreQueryFilters().AsNoTracking()
            .Where(e => e.OccurredOn >= range.From && e.OccurredOn <= range.To)
            .GroupBy(e => e.Category!.IsSystem ? (Guid?)e.CategoryId : null)
            .Select(g => new { CategoryId = g.Key, Total = g.Sum(e => e.Amount), Count = g.Count() })
            .ToListAsync(ct);

        var grandTotal = rows.Sum(r => r.Total);
        var system = SystemCategories.All.ToDictionary(c => c.Id);

        var categories = rows
            .Select(r =>
            {
                var known = r.CategoryId is { } id && system.TryGetValue(id, out var c) ? c : null;
                return new SystemCategoryTotalDto(
                    known?.Name ?? CustomBucketName,
                    known?.ColorHex ?? CustomBucketColor,
                    r.Total,
                    r.Count,
                    ReportMath.Percent(r.Total, grandTotal));
            })
            .OrderByDescending(c => c.Total)
            .ToList();

        return new SystemCategoryTotalsDto(range.From, range.To, grandTotal, rows.Sum(r => r.Count), categories);
    }

    public async Task<SystemMonthlyTotalsDto> MonthlyAsync(SystemMonthlyQuery query, CancellationToken ct)
    {
        var year = query.Year ?? clock.Today.Year;
        var start = new DateOnly(year, 1, 1);
        var end = start.AddYears(1);

        var rows = await db.Expenses.IgnoreQueryFilters().AsNoTracking()
            .Where(e => e.OccurredOn >= start && e.OccurredOn < end)
            .GroupBy(e => e.OccurredOn.Month)
            .Select(g => new { Month = g.Key, Total = g.Sum(e => e.Amount), Count = g.Count() })
            .ToListAsync(ct);

        var byMonth = rows.ToDictionary(r => r.Month);
        var months = Enumerable.Range(1, 12)
            .Select(m => byMonth.TryGetValue(m, out var r) ? new SystemMonthTotalDto(m, r.Total, r.Count) : new SystemMonthTotalDto(m, 0, 0))
            .ToList();

        return new SystemMonthlyTotalsDto(year, months);
    }
}

public sealed class GetSystemTotalsEndpoint : IFeatureEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admin/reports/category-totals", async ([AsParameters] SystemTotalsQuery query, GetSystemTotalsHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.CategoryTotalsAsync(query, ct)))
            .WithValidation<SystemTotalsQuery>()
            .RequireAuthorization(Policies.RequireAdmin)
            .WithTags("Admin")
            .WithName("GetSystemCategoryTotals")
            .Produces<SystemCategoryTotalsDto>();

        app.MapGet("/api/admin/reports/monthly", async ([AsParameters] SystemMonthlyQuery query, GetSystemTotalsHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.MonthlyAsync(query, ct)))
            .WithValidation<SystemMonthlyQuery>()
            .RequireAuthorization(Policies.RequireAdmin)
            .WithTags("Admin")
            .WithName("GetSystemMonthlyTotals")
            .Produces<SystemMonthlyTotalsDto>();
    }
}
