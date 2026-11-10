using FluentValidation;

namespace SpendScope.Features.Common;

/// <summary>Resolves optional from/to query values with the spec 14.4 defaults.</summary>
public readonly record struct DateRange(DateOnly From, DateOnly To)
{
    /// <summary>Spec 16.3: at most 366 days for summaries and expense lists.</summary>
    public const int MaxDays = 366;

    public int Days => To.DayNumber - From.DayNumber + 1;

    /// <summary>
    /// Defaults: <c>to</c> = last day of the current month, <c>from</c> = first day of <c>to</c>'s
    /// month. Spec 14.4 says "to = today", but "today" on the server is a UTC date: for users east
    /// of UTC that cut off expenses dated today in their own time zone (and the spec allows
    /// future-dated expenses). Ending at the month's end avoids both. Clients that care about an
    /// exact range send both values; the Blazor app always does.
    /// </summary>
    public static DateRange Resolve(DateOnly? from, DateOnly? to, DateOnly today)
    {
        var end = to ?? new DateOnly(today.Year, today.Month, 1).AddMonths(1).AddDays(-1);
        var start = from ?? new DateOnly(end.Year, end.Month, 1);
        return new DateRange(start, end);
    }
}

public static class DateRangeRules
{
    /// <summary>Adds "from ≤ to" and "range ≤ 366 days" rules on the resolved range.</summary>
    public static void ValidDateRange<T>(this AbstractValidator<T> validator, Func<T, DateRange> resolve)
    {
        validator.RuleFor(x => resolve(x))
            .Must(r => r.From <= r.To)
            .WithName("from")
            .OverridePropertyName("from")
            .WithErrorCode("range_invalid")
            .WithMessage("'from' must be on or before 'to'.");

        validator.RuleFor(x => resolve(x))
            .Must(r => r.From > r.To || r.Days <= DateRange.MaxDays)
            .OverridePropertyName("to")
            .WithErrorCode("range_too_large")
            .WithMessage($"Date range can be at most {DateRange.MaxDays} days.");
    }
}
