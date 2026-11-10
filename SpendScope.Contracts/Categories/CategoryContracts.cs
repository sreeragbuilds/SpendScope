namespace SpendScope.Contracts.Categories;

/// <param name="ExpenseCount">How many of the caller's expenses use this category.</param>
public sealed record CategoryDto(Guid Id, string Name, string ColorHex, bool IsSystem, int ExpenseCount);

public sealed record CreateCategoryRequest(string Name, string ColorHex);

public sealed record UpdateCategoryRequest(string Name, string ColorHex);
