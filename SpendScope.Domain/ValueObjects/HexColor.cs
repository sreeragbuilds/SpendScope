using SpendScope.Domain.Common;

namespace SpendScope.Domain.ValueObjects;

/// <summary>A color in <c>#RRGGBB</c> form, stored uppercase.</summary>
public readonly record struct HexColor
{
    public HexColor(string value)
    {
        if (!IsValid(value))
        {
            throw new DomainException("color_invalid", "Color must be in the form #RRGGBB.");
        }

        Value = value.ToUpperInvariant();
    }

    public string Value { get; }

    public static bool IsValid(string? value) =>
        value is { Length: 7 } && value[0] == '#' && value.AsSpan(1).ContainsAnyExcept(HexDigits) is false;

    private static readonly System.Buffers.SearchValues<char> HexDigits =
        System.Buffers.SearchValues.Create("0123456789abcdefABCDEF");

    public override string ToString() => Value;
}
