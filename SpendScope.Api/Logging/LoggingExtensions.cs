using System.Security.Claims;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using SpendScope.Infrastructure.Identity;

namespace SpendScope.Api.Logging;

/// <summary>Serilog setup (spec sections 20.1 and 20.2).</summary>
internal static class LoggingExtensions
{
    private const string DevelopmentTemplate =
        "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}";

    /// <summary>
    /// Levels and overrides come from the "Serilog" configuration section. The sink is chosen in
    /// code: readable text in Development, JSON everywhere else.
    /// </summary>
    public static WebApplicationBuilder AddSpendScopeLogging(this WebApplicationBuilder builder)
    {
        var isDevelopment = builder.Environment.IsDevelopment();
        var environmentName = builder.Environment.EnvironmentName;

        builder.Services.AddSerilog((services, logger) =>
        {
            logger
                .ReadFrom.Configuration(builder.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                // Not Enrich.WithEnvironmentName(): that reads the ASPNETCORE_ENVIRONMENT variable and
                // reports "Production" when the environment is set any other way (e.g. in tests).
                .Enrich.WithProperty("EnvironmentName", environmentName);

            if (isDevelopment)
            {
                logger.WriteTo.Console(outputTemplate: DevelopmentTemplate, formatProvider: null);
            }
            else
            {
                logger.WriteTo.Console(new RenderedCompactJsonFormatter());
            }
        });

        return builder;
    }

    /// <summary>
    /// One log event per request: method, path (never the query string, which can carry
    /// personal filter values), status code, elapsed ms, and the user ID when signed in.
    /// </summary>
    public static IApplicationBuilder UseSpendScopeRequestLogging(this IApplicationBuilder app) =>
        app.UseSerilogRequestLogging(options =>
        {
            options.IncludeQueryInRequestPath = false;

            options.EnrichDiagnosticContext = (diagnostics, http) =>
            {
                var userId = http.User.FindFirstValue(TokenClaims.Subject);
                if (userId is not null)
                {
                    diagnostics.Set("UserId", userId);
                }
            };

            // Probes hit /health every few seconds; keep them out of Information-level logs
            // unless they fail.
            options.GetLevel = (http, _, exception) =>
                exception is not null || http.Response.StatusCode >= StatusCodes.Status500InternalServerError
                    ? LogEventLevel.Error
                    : http.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase)
                        ? LogEventLevel.Debug
                        : LogEventLevel.Information;
        });
}
