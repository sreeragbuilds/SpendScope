using SpendScope.Domain.Common;

namespace SpendScope.Domain.ValueObjects;

/// <summary>An amount paired with an ISO 4217 currency code. Equality uses both.</summary>
public readonly record struct Money
{
    public Money(decimal amount, string currency)
    {
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3 || !currency.All(char.IsAsciiLetterUpper))
        {
            throw new DomainException("currency_invalid", "Currency must be a 3-letter uppercase ISO 4217 code.");
        }

        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }

    public string Currency { get; }

    /// <summary>Adds two amounts of the same currency. Mixing currencies is a programming error.</summary>
    public Money Add(Money other)
    {
        if (other.Currency != Currency)
        {
            throw new DomainException("currency_mismatch", $"Cannot add {other.Currency} to {Currency}.");
        }

        return new Money(Amount + other.Amount, Currency);
    }

    public static Money operator +(Money left, Money right) => left.Add(right);

    public override string ToString() => $"{Amount:0.00} {Currency}";
}
