using SpendScope.Domain.Common;

namespace SpendScope.Domain.ValueObjects;

/// <summary>A syntactically plausible email address (one @, non-empty local part and domain, max 256).</summary>
public readonly record struct EmailAddress
{
    public const int MaxLength = 256;

    public EmailAddress(string value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (!IsValid(trimmed))
        {
            throw new DomainException("email_invalid", "Email address is not valid.");
        }

        Value = trimmed;
    }

    public string Value { get; }

    public static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaxLength || value.Any(char.IsWhiteSpace))
        {
            return false;
        }

        var at = value.IndexOf('@', StringComparison.Ordinal);
        return at > 0
            && at == value.LastIndexOf('@')
            && at < value.Length - 1
            && value.IndexOf('.', at) > at + 1
            && !value.EndsWith('.');
    }

    public override string ToString() => Value;
}
