using SpendScope.Domain.Categories;
using SpendScope.Domain.Common;
using SpendScope.Domain.ValueObjects;

namespace SpendScope.Domain.Expenses;

/// <summary>A single spending record owned by one user.</summary>
public sealed class Expense
{
    private Expense()
    {
        // For EF Core.
        Currency = string.Empty;
    }

    private Expense(Guid id, Guid userId, Money money, DateOnly occurredOn, Guid categoryId, string? note, DateTime createdAtUtc)
    {
        Id = id;
        UserId = userId;
        Amount = money.Amount;
        Currency = money.Currency;
        OccurredOn = occurredOn;
        CategoryId = categoryId;
        Note = note;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    /// <summary>Always taken from the access token, never from the client.</summary>
    public Guid UserId { get; private set; }

    public decimal Amount { get; private set; }

    /// <summary>Copied from the user's default currency at creation. Not editable afterwards.</summary>
    public string Currency { get; private set; }

    public DateOnly OccurredOn { get; private set; }

    public Guid CategoryId { get; private set; }

    public Category? Category { get; private set; }

    public string? Note { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? UpdatedAtUtc { get; private set; }

    public Money Money => new(Amount, Currency);

    public static Expense Create(Guid userId, Money money, DateOnly occurredOn, Guid categoryId, string? note, DateTime nowUtc)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("user_required", "An expense must belong to a user.");
        }

        if (categoryId == Guid.Empty)
        {
            throw new DomainException("category_required", "An expense must have a category.");
        }

        EnsureValidAmount(money.Amount);
        EnsureValidDate(occurredOn, nowUtc);

        return new Expense(Guid.CreateVersion7(), userId, money, occurredOn, categoryId, NormalizeNote(note), nowUtc);
    }

    /// <summary>Replaces every editable field. Currency is intentionally not editable.</summary>
    public void Update(decimal amount, DateOnly occurredOn, Guid categoryId, string? note, DateTime nowUtc)
    {
        if (categoryId == Guid.Empty)
        {
            throw new DomainException("category_required", "An expense must have a category.");
        }

        EnsureValidAmount(amount);
        EnsureValidDate(occurredOn, nowUtc);

        Amount = amount;
        OccurredOn = occurredOn;
        CategoryId = categoryId;
        Note = NormalizeNote(note);
        UpdatedAtUtc = nowUtc;
    }

    public void ChangeAmount(decimal amount, DateTime nowUtc)
    {
        EnsureValidAmount(amount);
        Amount = amount;
        UpdatedAtUtc = nowUtc;
    }

    private static void EnsureValidAmount(decimal amount)
    {
        if (!ExpenseRules.IsValidAmount(amount))
        {
            throw new DomainException(
                "amount_range",
                $"Amount must be greater than 0, at most {ExpenseRules.MaxAmount:N2}, with at most two decimals.");
        }
    }

    private static void EnsureValidDate(DateOnly occurredOn, DateTime nowUtc)
    {
        if (!ExpenseRules.IsValidDate(occurredOn, DateOnly.FromDateTime(nowUtc)))
        {
            throw new DomainException("date_future", "Date cannot be more than 1 year in the future.");
        }
    }

    private static string? NormalizeNote(string? note)
    {
        var trimmed = note?.Trim();
        if (trimmed is { Length: > ExpenseRules.NoteMaxLength })
        {
            throw new DomainException("note_length", $"Note must be at most {ExpenseRules.NoteMaxLength} characters.");
        }

        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
