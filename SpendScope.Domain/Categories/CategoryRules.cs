namespace SpendScope.Domain.Categories;

public static class CategoryRules
{
    public const int NameMaxLength = 40;

    /// <summary>Uppercase invariant, trimmed form used for case-insensitive uniqueness.</summary>
    public static string Normalize(string name) => name.Trim().ToUpperInvariant();

    public static bool IsValidName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var trimmed = name.Trim();
        return trimmed.Length <= NameMaxLength && !trimmed.Any(char.IsControl);
    }
}
