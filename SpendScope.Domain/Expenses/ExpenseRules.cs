namespace SpendScope.Domain.Expenses;

/// <summary>Limits shared by the domain invariants and the request validators (spec FR-EXP-02/03).</summary>
public static class ExpenseRules
{
    public const decimal MaxAmount = 1_000_000.00m;
    public const int MaxFutureDays = 365;
    public const int NoteMaxLength = 500;
    public const int AmountScale = 2;

    public static bool IsValidAmount(decimal amount) =>
        amount > 0 && amount <= MaxAmount && decimal.Round(amount, AmountScale) == amount;

    public static bool IsValidDate(DateOnly occurredOn, DateOnly today) =>
        occurredOn <= today.AddDays(MaxFutureDays);
}
