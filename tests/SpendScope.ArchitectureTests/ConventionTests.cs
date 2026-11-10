using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using SpendScope.Features;
using static SpendScope.ArchitectureTests.Assemblies;

namespace SpendScope.ArchitectureTests;

/// <summary>Naming, placement and EF Core confinement rules (ARCH-07 to ARCH-09).</summary>
public class ConventionTests
{
    private const string EfCore = "Microsoft.EntityFrameworkCore";

    [Fact]
    public void Handlers_live_in_Features()
    {
        // ARCH-07. Framework plumbing that is also called "*Handler" by convention is excluded:
        // GlobalExceptionHandler (IExceptionHandler), authorization handlers, and the Blazor
        // client's DelegatingHandlers. See ADR 0002.
        var result = ArchAssert.OwnTypes(All)
            .And().AreClasses()
            .And().HaveNameEndingWith("Handler", StringComparison.Ordinal)
            .And().DoNotImplementInterface(typeof(IExceptionHandler))
            .And().DoNotImplementInterface(typeof(IAuthorizationHandler))
            .And().DoNotInherit(typeof(HttpMessageHandler))
            .Should().ResideInNamespaceStartingWith(FeaturesNs + ".")
            .GetResult();

        ArchAssert.Passes(result, "ARCH-07");
    }

    [Fact]
    public void Endpoints_are_public_and_in_Features()
    {
        // ARCH-08
        var result = ArchAssert.OwnTypes(All)
            .And().AreClasses()
            .And().ImplementInterface(typeof(IFeatureEndpoint))
            .Should().BePublic()
            .And().NotBeAbstract()
            .And().ResideInNamespaceStartingWith(FeaturesNs + ".")
            .GetResult();

        ArchAssert.Passes(result, "ARCH-08");
    }

    [Fact]
    public void EntityFrameworkCore_is_not_used_by_Domain_Contracts_or_Web()
    {
        // ARCH-09: only Api and Infrastructure own EF Core.
        var result = ArchAssert.OwnTypes(DomainAssembly, ContractsAssembly, WebClientAssembly)
            .ShouldNot().HaveDependencyOn(EfCore)
            .GetResult();

        ArchAssert.Passes(result, "ARCH-09");
    }

    [Fact]
    public void EntityFrameworkCore_in_Application_is_limited_to_abstractions()
    {
        // ARCH-09: IAppDbContext exposes DbSet<T>, so Application.Abstractions may name EF types.
        var result = ArchAssert.OwnTypes(ApplicationAssembly)
            .And().DoNotResideInNamespace(ApplicationNs + ".Abstractions")
            .ShouldNot().HaveDependencyOn(EfCore)
            .GetResult();

        ArchAssert.Passes(result, "ARCH-09");
    }

    [Fact]
    public void EntityFrameworkCore_in_Features_is_limited_to_handlers()
    {
        // ARCH-09: "query code" means handlers (async LINQ operators such as FirstOrDefaultAsync).
        // Requests, validators, responses and endpoints stay EF-free.
        var result = ArchAssert.OwnTypes(FeaturesAssembly)
            .And().DoNotHaveNameEndingWith("Handler", StringComparison.Ordinal)
            .ShouldNot().HaveDependencyOn(EfCore)
            .GetResult();

        ArchAssert.Passes(result, "ARCH-09");
    }

    [Fact]
    public void Features_never_uses_a_concrete_DbContext()
    {
        // Spec 10.2: Features must not use DbContext directly; it goes through IAppDbContext.
        var result = ArchAssert.OwnTypes(FeaturesAssembly)
            .ShouldNot().HaveDependencyOn(EfCore + ".DbContext")
            .GetResult();

        ArchAssert.Passes(result, "ARCH-09");
    }
}
