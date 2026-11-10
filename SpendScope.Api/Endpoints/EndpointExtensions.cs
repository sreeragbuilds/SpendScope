using System.Reflection;
using SpendScope.Features;

namespace SpendScope.Api.Endpoints;

internal static class EndpointExtensions
{
    /// <summary>Maps every concrete <see cref="IFeatureEndpoint"/> found in <paramref name="assembly"/>.</summary>
    public static IEndpointRouteBuilder MapFeatureEndpoints(this IEndpointRouteBuilder app, Assembly assembly)
    {
        var endpoints = assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(IFeatureEndpoint).IsAssignableFrom(t))
            .Select(t => (IFeatureEndpoint)Activator.CreateInstance(t)!);

        foreach (var endpoint in endpoints)
        {
            endpoint.Map(app);
        }

        return app;
    }
}
