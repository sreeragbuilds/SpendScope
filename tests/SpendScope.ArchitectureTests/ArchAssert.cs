using NetArchTest.Rules;

namespace SpendScope.ArchitectureTests;

internal static class ArchAssert
{
    /// <summary>Fails with the rule ID and the offending type names, so a broken build says what to fix.</summary>
    public static void Passes(TestResult result, string rule)
    {
        var failing = result.FailingTypeNames ?? [];
        Assert.True(
            result.IsSuccessful,
            $"{rule} violated by: {string.Join(", ", failing)}");
    }

    /// <summary>Types we wrote: everything in the assemblies except compiler-emitted helpers.</summary>
    public static PredicateList OwnTypes(params System.Reflection.Assembly[] assemblies)
    {
        var predicates = Types.InAssemblies(assemblies).That();
        var list = predicates.DoNotResideInNamespace(Assemblies.CompilerGeneratedNamespaces[0]);
        foreach (var ns in Assemblies.CompilerGeneratedNamespaces.Skip(1))
        {
            list = list.And().DoNotResideInNamespace(ns);
        }

        return list;
    }
}
