namespace SpendScope.Features.Common;

/// <summary>Arithmetic shared by user and admin reports.</summary>
public static class ReportMath
{
    /// <summary>part ÷ total × 100, rounded to 2 decimals; 0 when total is 0 (spec 16.1).</summary>
    public static decimal Percent(decimal part, decimal total) =>
        total == 0 ? 0 : decimal.Round(part / total * 100, 2, MidpointRounding.AwayFromZero);

    public static decimal Average(decimal total, int count) =>
        count == 0 ? 0 : decimal.Round(total / count, 2, MidpointRounding.AwayFromZero);
}
