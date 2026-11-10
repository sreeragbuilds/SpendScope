using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace SpendScope.Features;

public static class DependencyInjection
{
    /// <summary>Registers every slice handler (scoped) and every validator in this assembly.</summary>
    public static IServiceCollection AddFeatures(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        var handlers = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.Name.EndsWith("Handler", StringComparison.Ordinal));
        foreach (var handler in handlers)
        {
            services.AddScoped(handler);
        }

        services.AddValidatorsFromAssembly(assembly, ServiceLifetime.Scoped);

        return services;
    }
}
