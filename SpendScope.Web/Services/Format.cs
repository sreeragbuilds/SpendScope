using System.Globalization;

namespace SpendScope.Web.Services;

/// <summary>Display formatting (spec 19.9).</summary>
public static class Format
{
    private static readonly Dictionary<string, (string Symbol, int Decimals)> Currencies = new(StringComparer.Ordinal)
    {
        ["USD"] = ("$", 2),
        ["EUR"] = ("€", 2),
        ["GBP"] = ("£", 2),
        ["INR"] = ("₹", 2),
        ["JPY"] = ("¥", 0),
        ["AUD"] = ("A$", 2),
        ["CAD"] = ("C$", 2),
    };

    public static string Money(decimal amount, string? currency)
    {
        var (symbol, decimals) = currency is not null && Currencies.TryGetValue(currency, out var c) ? c : (currency + " ", 2);
        var number = Math.Abs(amount).ToString("N" + decimals, CultureInfo.CurrentCulture);
        return (amount < 0 ? "-" : string.Empty) + symbol + number;
    }

    public static string Date(DateOnly date) => date.ToString("d MMM yyyy", CultureInfo.CurrentCulture);

    public static string Date(DateTime date) => date.ToLocalTime().ToString("d MMM yyyy", CultureInfo.CurrentCulture);

    public static string Percent(decimal value) => value.ToString("0.0", CultureInfo.CurrentCulture) + "%";

    public static string MonthShort(int month) => CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(month);
}

/// <summary>Today in the browser's time zone, which is the user's notion of "today".</summary>
public static class LocalDate
{
    public static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    public static DateOnly FirstOfMonth(DateOnly d) => new(d.Year, d.Month, 1);

    public static DateOnly EndOfMonth(DateOnly d) => FirstOfMonth(d).AddMonths(1).AddDays(-1);

    public static DateOnly? Parse(string? value) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    public static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
