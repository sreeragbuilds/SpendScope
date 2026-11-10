using SpendScope.Domain.ValueObjects;

namespace SpendScope.Domain.Categories;

/// <summary>The ten default categories from spec section 8.4. IDs are fixed so seed migrations are stable.</summary>
public static class SystemCategories
{
    public static readonly Guid FoodAndDining = new("0196a3f3-1111-7d1a-9b2e-000000000001");
    public static readonly Guid Groceries = new("0196a3f3-1111-7d1a-9b2e-000000000002");
    public static readonly Guid Transport = new("0196a3f3-1111-7d1a-9b2e-000000000003");
    public static readonly Guid Housing = new("0196a3f3-1111-7d1a-9b2e-000000000004");
    public static readonly Guid Utilities = new("0196a3f3-1111-7d1a-9b2e-000000000005");
    public static readonly Guid Health = new("0196a3f3-1111-7d1a-9b2e-000000000006");
    public static readonly Guid Entertainment = new("0196a3f3-1111-7d1a-9b2e-000000000007");
    public static readonly Guid Shopping = new("0196a3f3-1111-7d1a-9b2e-000000000008");
    public static readonly Guid Education = new("0196a3f3-1111-7d1a-9b2e-000000000009");
    public static readonly Guid Other = new("0196a3f3-1111-7d1a-9b2e-00000000000a");

    private static readonly DateTime SeededAtUtc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static IReadOnlyList<Category> All { get; } =
    [
        Category.CreateSystem(FoodAndDining, "Food and Dining", new HexColor("#F59E0B"), SeededAtUtc),
        Category.CreateSystem(Groceries, "Groceries", new HexColor("#84CC16"), SeededAtUtc),
        Category.CreateSystem(Transport, "Transport", new HexColor("#3B82F6"), SeededAtUtc),
        Category.CreateSystem(Housing, "Housing", new HexColor("#8B5CF6"), SeededAtUtc),
        Category.CreateSystem(Utilities, "Utilities", new HexColor("#06B6D4"), SeededAtUtc),
        Category.CreateSystem(Health, "Health", new HexColor("#EF4444"), SeededAtUtc),
        Category.CreateSystem(Entertainment, "Entertainment", new HexColor("#EC4899"), SeededAtUtc),
        Category.CreateSystem(Shopping, "Shopping", new HexColor("#F97316"), SeededAtUtc),
        Category.CreateSystem(Education, "Education", new HexColor("#14B8A6"), SeededAtUtc),
        Category.CreateSystem(Other, "Other", new HexColor("#6B7280"), SeededAtUtc),
    ];
}
