using SpendScope.Domain.Common;
using SpendScope.Domain.ValueObjects;

namespace SpendScope.Domain.Categories;

/// <summary>
/// A label for expenses. System categories (UserId null) are visible to everyone and read only;
/// user categories belong to one user.
/// </summary>
public sealed class Category
{
    private Category()
    {
        // For EF Core.
        Name = string.Empty;
        NormalizedName = string.Empty;
        ColorHex = string.Empty;
    }

    private Category(Guid id, Guid? userId, string name, HexColor color, DateTime createdAtUtc)
    {
        Id = id;
        UserId = userId;
        IsSystem = userId is null;
        CreatedAtUtc = createdAtUtc;
        Name = string.Empty;
        NormalizedName = string.Empty;
        ColorHex = color.Value;
        SetName(name);
    }

    public Guid Id { get; private set; }

    public Guid? UserId { get; private set; }

    public string Name { get; private set; }

    public string NormalizedName { get; private set; }

    public string ColorHex { get; private set; }

    public bool IsSystem { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public static Category CreateForUser(Guid userId, string name, HexColor color, DateTime nowUtc)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("user_required", "A user category must belong to a user.");
        }

        return new Category(Guid.CreateVersion7(), userId, name, color, nowUtc);
    }

    /// <summary>Used only by seeding: system categories have fixed IDs so migrations are stable.</summary>
    public static Category CreateSystem(Guid id, string name, HexColor color, DateTime createdAtUtc) =>
        new(id, null, name, color, createdAtUtc);

    public bool IsOwnedBy(Guid userId) => !IsSystem && UserId == userId;

    public void Rename(string name)
    {
        EnsureEditable();
        SetName(name);
    }

    public void Recolor(HexColor color)
    {
        EnsureEditable();
        ColorHex = color.Value;
    }

    private void SetName(string name)
    {
        if (!CategoryRules.IsValidName(name))
        {
            throw new DomainException(
                "category_name_length",
                $"Category name must be 1 to {CategoryRules.NameMaxLength} characters.");
        }

        Name = name.Trim();
        NormalizedName = CategoryRules.Normalize(name);
    }

    private void EnsureEditable()
    {
        if (IsSystem)
        {
            throw new DomainException("system_category_read_only", "System categories cannot be changed.");
        }
    }
}
