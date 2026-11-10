namespace SpendScope.Contracts.Reports;

public sealed record CategoryTotalDto(Guid CategoryId, string Name, string ColorHex, decimal Total, int Count, decimal Percentage);

/// <summary>Spec 16.1. Categories with no spend are omitted; percentages may not sum to exactly 100.</summary>
public sealed record CategorySummaryDto(
    DateOnly From,
    DateOnly To,
    string Currency,
    decimal GrandTotal,
    int ExpenseCount,
    decimal AverageExpense,
    IReadOnlyList<CategoryTotalDto> Categories);

public sealed record MonthTotalDto(int Month, decimal Total, int Count);

/// <summary>Spec 16.2. Always 12 entries, zero-filled.</summary>
public sealed record MonthlyTotalsDto(int Year, string Currency, IReadOnlyList<MonthTotalDto> Months);
