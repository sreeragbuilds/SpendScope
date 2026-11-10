using System.Reflection;

namespace SpendScope.ArchitectureTests;

/// <summary>
/// The production assemblies under test, loaded by name. Every one is a project reference of
/// this test project, so it sits in the output folder even before it contains any types.
/// </summary>
internal static class Assemblies
{
    public const string DomainNs = "SpendScope.Domain";
    public const string ApplicationNs = "SpendScope.Application";
    public const string ContractsNs = "SpendScope.Contracts";
    public const string InfrastructureNs = "SpendScope.Infrastructure";
    public const string FeaturesNs = "SpendScope.Features";
    public const string ApiNs = "SpendScope.Api";
    public const string WebNs = "SpendScope.Web";

    public static readonly Assembly DomainAssembly = Assembly.Load(DomainNs);
    public static readonly Assembly ApplicationAssembly = Assembly.Load(ApplicationNs);
    public static readonly Assembly ContractsAssembly = Assembly.Load(ContractsNs);
    public static readonly Assembly InfrastructureAssembly = Assembly.Load(InfrastructureNs);
    public static readonly Assembly FeaturesAssembly = Assembly.Load(FeaturesNs);
    public static readonly Assembly ApiAssembly = Assembly.Load(ApiNs);
    public static readonly Assembly WebClientAssembly = Assembly.Load(WebNs);

    public static readonly Assembly[] All = [DomainAssembly, ApplicationAssembly, ContractsAssembly, InfrastructureAssembly, FeaturesAssembly, ApiAssembly, WebClientAssembly];

    /// <summary>
    /// Namespaces of types the C# compiler emits into an assembly (for example
    /// <c>NullableAttribute</c> and <c>EmbeddedAttribute</c>). They are not our code.
    /// </summary>
    public static readonly string[] CompilerGeneratedNamespaces =
    [
        "System.Runtime.CompilerServices",
        "Microsoft.CodeAnalysis",
    ];
}
