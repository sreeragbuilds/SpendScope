namespace SpendScope.Contracts.Admin;

/// <summary>Spec 15.20. Deliberately carries no expense data.</summary>
public sealed record AdminUserDto(Guid Id, string Email, string DisplayName, IReadOnlyList<string> Roles, bool IsLocked, DateTime CreatedAtUtc);

/// <summary>A system-wide aggregate row. Never carries a user ID (spec 16.5).</summary>
/// <param name="Name">A system category name, or "Custom categories" for all user-defined ones combined.</param>
public sealed record SystemCategoryTotalDto(string Name, string ColorHex, decimal Total, int Count, decimal Percentage);

public sealed record SystemCategoryTotalsDto(DateOnly From, DateOnly To, decimal GrandTotal, int ExpenseCount, IReadOnlyList<SystemCategoryTotalDto> Categories);

public sealed record SystemMonthTotalDto(int Month, decimal Total, int Count);

public sealed record SystemMonthlyTotalsDto(int Year, IReadOnlyList<SystemMonthTotalDto> Months);
