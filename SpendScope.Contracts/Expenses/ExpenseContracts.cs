namespace SpendScope.Contracts.Expenses;

public sealed record CategoryRef(Guid Id, string Name, string ColorHex);

public sealed record ExpenseDto(
    Guid Id,
    decimal Amount,
    string Currency,
    DateOnly OccurredOn,
    CategoryRef Category,
    string? Note,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed record CreateExpenseRequest(decimal Amount, DateOnly OccurredOn, Guid CategoryId, string? Note);

/// <summary>The expense ID comes from the route, never the body.</summary>
public sealed record UpdateExpenseRequest(decimal Amount, DateOnly OccurredOn, Guid CategoryId, string? Note);

/// <summary>Query parameters for listing and exporting expenses (spec 14.4).</summary>
public sealed record ExpenseQuery
{
    public DateOnly? From { get; init; }

    public DateOnly? To { get; init; }

    public Guid? CategoryId { get; init; }

    public decimal? MinAmount { get; init; }

    public decimal? MaxAmount { get; init; }

    /// <summary>date, amount, or category.</summary>
    public string? SortBy { get; init; }

    /// <summary>asc or desc.</summary>
    public string? SortDir { get; init; }

    public int? Page { get; init; }

    public int? PageSize { get; init; }

    public string ToQueryString()
    {
        var parts = new List<string>();
        Add(parts, "from", From?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        Add(parts, "to", To?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        Add(parts, "categoryId", CategoryId?.ToString());
        Add(parts, "minAmount", MinAmount?.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Add(parts, "maxAmount", MaxAmount?.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Add(parts, "sortBy", SortBy);
        Add(parts, "sortDir", SortDir);
        Add(parts, "page", Page?.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Add(parts, "pageSize", PageSize?.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return parts.Count == 0 ? string.Empty : "?" + string.Join('&', parts);

        static void Add(List<string> parts, string name, string? value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                parts.Add($"{name}={Uri.EscapeDataString(value)}");
            }
        }
    }
}
