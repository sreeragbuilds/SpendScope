using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SpendScope.Infrastructure.Persistence;

namespace SpendScope.Api.HealthChecks;

/// <summary>Liveness and readiness probes (spec section 20.4).</summary>
internal static class HealthCheckExtensions
{
    private const string ReadyTag = "ready";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IServiceCollection AddSpendScopeHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        // Same resolution as AddInfrastructure, which has already failed fast if it is missing.
        var connectionString = DatabaseConnection.Resolve(configuration)
            ?? throw new InvalidOperationException("Connection string 'ConnectionStrings:Default' is not configured.");

        services.AddHealthChecks()
            .AddNpgSql(
                connectionString,
                name: "postgres",
                tags: [ReadyTag],
                timeout: TimeSpan.FromSeconds(3));

        return services;
    }

    public static IEndpointRouteBuilder MapSpendScopeHealthChecks(this IEndpointRouteBuilder app)
    {
        // live: the process is up and serving requests. Runs no checks, so a database outage
        // never makes the orchestrator restart a healthy process.
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = WriteResponseAsync,
        }).AllowAnonymous();

        // ready: dependencies are reachable; returns 503 when any "ready" check fails.
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(ReadyTag),
            ResponseWriter = WriteResponseAsync,
        }).AllowAnonymous();

        return app;
    }

    /// <summary>
    /// Small JSON body with each check's name, status and duration. Exception details are
    /// deliberately left out: these endpoints are anonymous.
    /// </summary>
    private static Task WriteResponseAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var body = new
        {
            status = report.Status.ToString(),
            totalDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 1),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                durationMs = Math.Round(e.Value.Duration.TotalMilliseconds, 1),
            }),
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOptions));
    }
}
