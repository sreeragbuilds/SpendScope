using static SpendScope.ArchitectureTests.Assemblies;

namespace SpendScope.ArchitectureTests;

/// <summary>Layer dependency rules from spec section 10.3.</summary>
public class LayerDependencyTests
{
    [Fact]
    public void Domain_has_no_dependency_on_other_projects()
    {
        // ARCH-01, type level: Domain code may only use System.* and itself.
        var result = ArchAssert.OwnTypes(DomainAssembly)
            .Should().OnlyHaveDependenciesOn("System", DomainNs)
            .GetResult();

        ArchAssert.Passes(result, "ARCH-01");
    }

    [Fact]
    public void Domain_references_no_assembly_outside_the_base_class_library()
    {
        // ARCH-01, assembly level: catches a package reference (EF Core, ASP.NET, ...) even if
        // only an attribute from it is used.
        var offenders = DomainAssembly.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(n => !n.StartsWith("System", StringComparison.Ordinal)
                     && n is not "netstandard" and not "mscorlib")
            .ToList();

        Assert.True(offenders.Count == 0, $"ARCH-01 violated: Domain references {string.Join(", ", offenders)}");
    }

    [Fact]
    public void Application_depends_only_on_Domain()
    {
        // ARCH-02
        var result = ArchAssert.OwnTypes(ApplicationAssembly)
            .ShouldNot().HaveDependencyOnAny(ContractsNs, InfrastructureNs, FeaturesNs, ApiNs, WebNs)
            .GetResult();

        ArchAssert.Passes(result, "ARCH-02");
    }

    [Fact]
    public void Features_does_not_reference_Infrastructure()
    {
        // ARCH-03: Features may use Application, Domain and Contracts only.
        var result = ArchAssert.OwnTypes(FeaturesAssembly)
            .ShouldNot().HaveDependencyOnAny(InfrastructureNs, ApiNs, WebNs)
            .GetResult();

        ArchAssert.Passes(result, "ARCH-03");
    }

    [Fact]
    public void Infrastructure_does_not_reference_Features()
    {
        // ARCH-05
        var result = ArchAssert.OwnTypes(InfrastructureAssembly)
            .ShouldNot().HaveDependencyOnAny(FeaturesNs, ApiNs, WebNs)
            .GetResult();

        ArchAssert.Passes(result, "ARCH-05");
    }

    [Fact]
    public void Contracts_has_no_dependency_on_other_projects()
    {
        // Spec 10.2: Contracts must not contain any logic or references.
        var result = ArchAssert.OwnTypes(ContractsAssembly)
            .ShouldNot().HaveDependencyOnAny(DomainNs, ApplicationNs, InfrastructureNs, FeaturesNs, ApiNs, WebNs)
            .GetResult();

        ArchAssert.Passes(result, "Contracts isolation");
    }

    [Fact]
    public void Web_does_not_reference_server_projects()
    {
        // ARCH-06
        var result = ArchAssert.OwnTypes(WebClientAssembly)
            .ShouldNot().HaveDependencyOnAny(DomainNs, ApplicationNs, FeaturesNs, InfrastructureNs, ApiNs)
            .GetResult();

        ArchAssert.Passes(result, "ARCH-06");
    }

    [Fact]
    public void Api_does_not_use_Web_types()
    {
        // ADR 0001: Api references Web only to host its static WASM bundle, never its code.
        var result = ArchAssert.OwnTypes(ApiAssembly)
            .ShouldNot().HaveDependencyOn(WebNs)
            .GetResult();

        ArchAssert.Passes(result, "ADR-0001");
    }
}
