using System.Linq.Expressions;
using FluentValidation;
using SpendScope.Contracts.Expenses;
using SpendScope.Domain.Common;
using SpendScope.Domain.Expenses;
using SpendScope.Features.Common;

namespace SpendScope.Features.Expenses;

/// <summary>
/// Filtering, sorting and projection shared by ListExpenses and ExportExpensesCsv. Plain LINQ
/// over <see cref="IQueryable{T}"/>: no EF Core types, so it may live outside a handler (ARCH-09).
/// </summary>
public static class ExpenseQueries
{
    public static readonly string[] SortFields = ["date", "amount", "category"];

    public static readonly Expression<Func<Expense, ExpenseDto>> ToDto = e => new ExpenseDto(
        e.Id,
        e.Amount,
        e.Currency,
        e.OccurredOn,
        new CategoryRef(e.Category!.Id, e.Category.Name, e.Category.ColorHex),
        e.Note,
        e.CreatedAtUtc,
        e.UpdatedAtUtc);

    public static IQueryable<Expense> ApplyFilters(this IQueryable<Expense> query, ExpenseQuery filter, DateRange range)
    {
        query = query.Where(e => e.OccurredOn >= range.From && e.OccurredOn <= range.To);

        if (filter.CategoryId is { } categoryId)
        {
            query = query.Where(e => e.CategoryId == categoryId);
        }

        if (filter.MinAmount is { } min)
        {
            query = query.Where(e => e.Amount >= min);
        }

        if (filter.MaxAmount is { } max)
        {
            query = query.Where(e => e.Amount <= max);
        }

        return query;
    }

    /// <summary>Whitelisted sort with a stable secondary sort on Id (spec 15.12).</summary>
    public static IQueryable<Expense> ApplySort(this IQueryable<Expense> query, ExpenseQuery filter)
    {
        var descending = !string.Equals(filter.SortDir, "asc", StringComparison.OrdinalIgnoreCase);
        var sortBy = filter.SortBy?.ToLowerInvariant() ?? "date";

        var ordered = (sortBy, descending) switch
        {
            ("amount", true) => query.OrderByDescending(e => e.Amount),
            ("amount", false) => query.OrderBy(e => e.Amount),
            ("category", true) => query.OrderByDescending(e => e.Category!.Name),
            ("category", false) => query.OrderBy(e => e.Category!.Name),
            (_, true) => query.OrderByDescending(e => e.OccurredOn),
            (_, false) => query.OrderBy(e => e.OccurredOn),
        };

        return descending ? ordered.ThenByDescending(e => e.Id) : ordered.ThenBy(e => e.Id);
    }
}

/// <summary>Validates <see cref="ExpenseQuery"/> for both list and export (spec 14.4).</summary>
public sealed class ExpenseQueryValidator : AbstractValidator<ExpenseQuery>
{
    public ExpenseQueryValidator(IClock clock)
    {
        this.ValidDateRange(q => DateRange.Resolve(q.From, q.To, clock.Today));

        RuleFor(x => x.MinAmount).GreaterThanOrEqualTo(0).WithErrorCode("amount_range");
        RuleFor(x => x.MaxAmount).GreaterThanOrEqualTo(0).WithErrorCode("amount_range");
        RuleFor(x => x)
            .Must(q => q.MinAmount is null || q.MaxAmount is null || q.MinAmount <= q.MaxAmount)
            .OverridePropertyName("minAmount")
            .WithErrorCode("amount_range")
            .WithMessage("'minAmount' must be less than or equal to 'maxAmount'.");

        RuleFor(x => x.SortBy)
            .Must(s => s is null || ExpenseQueries.SortFields.Contains(s.ToLowerInvariant()))
            .WithErrorCode("sort_invalid")
            .WithMessage($"sortBy must be one of: {string.Join(", ", ExpenseQueries.SortFields)}.");
        RuleFor(x => x.SortDir)
            .Must(s => s is null || s.Equals("asc", StringComparison.OrdinalIgnoreCase) || s.Equals("desc", StringComparison.OrdinalIgnoreCase))
            .WithErrorCode("sort_invalid")
            .WithMessage("sortDir must be 'asc' or 'desc'.");

        RuleFor(x => x.Page).ValidPage();
        RuleFor(x => x.PageSize).ValidPageSize();
    }
}
