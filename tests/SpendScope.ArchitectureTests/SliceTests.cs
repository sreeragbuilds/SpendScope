using System.Text.RegularExpressions;
using NetArchTest.Rules;
using static SpendScope.ArchitectureTests.Assemblies;

namespace SpendScope.ArchitectureTests;

/// <summary>Vertical slice isolation (ARCH-04).</summary>
public partial class SliceTests
{
    // A slice is a namespace SpendScope.Features.{Area}.{Slice}, e.g. SpendScope.Features.Expenses.CreateExpense.
    [GeneratedRegex(@"^SpendScope\.Features\.[^.]+\.[^.]+")]
    private static partial Regex SliceNamespace();

    internal static IReadOnlyList<string> DiscoverSlices() =>
        FeaturesAssembly.GetTypes()
            .Select(t => t.Namespace is null ? null : SliceNamespace().Match(t.Namespace))
            .Where(m => m is { Success: true })
            .Select(m => m!.Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

    [Fact]
    public void Slices_do_not_reference_each_other()
    {
        var slices = DiscoverSlices();
        var violations = new List<string>();

        foreach (var slice in slices)
        {
            // NetArchTest matches whole namespace segments, so "CreateExpense" does not also match a
            // sibling slice named "CreateExpenseCsv". (A trailing "." would match nothing at all.)
            var others = slices.Where(s => s != slice).ToArray();
            if (others.Length == 0)
            {
                continue;
            }

            var result = Types.InAssembly(FeaturesAssembly)
                .That().ResideInNamespaceMatching($"^{Regex.Escape(slice)}(\\.|$)")
                .ShouldNot().HaveDependencyOnAny(others)
                .GetResult();

            if (!result.IsSuccessful)
            {
                violations.AddRange(result.FailingTypeNames.Select(t => $"{t} (in {slice})"));
            }
        }

        Assert.True(
            violations.Count == 0,
            $"ARCH-04 violated; share code through Application instead: {string.Join(", ", violations)}");
    }
}
