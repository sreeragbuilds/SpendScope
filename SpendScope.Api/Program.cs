using Microsoft.AspNetCore.HttpOverrides;
using Serilog;
using SpendScope.Api.Auth;
using SpendScope.Api.Endpoints;
using SpendScope.Api.HealthChecks;
using SpendScope.Api.Logging;
using SpendScope.Api.Middleware;
using SpendScope.Api.RateLimiting;
using SpendScope.Application.Common;
using SpendScope.Features;
using SpendScope.Infrastructure;
using SpendScope.Infrastructure.Persistence.Seeding;

// Startup logger: catches failures before the host's logger is built (bad config, missing secrets).
// AddSerilog replaces it once the host starts.
Log.Logger = new LoggerConfiguration().WriteTo.Console(formatProvider: null).CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.AddSpendScopeLogging();

    builder.Services
        .AddInfrastructure(builder.Configuration, builder.Environment)
        .AddFeatures()
        .AddSpendScopeAuth()
        .AddSpendScopeRateLimiting(builder.Configuration)
        .AddSpendScopeHealthChecks(builder.Configuration)
        .AddOpenApi()
        .AddProblemDetails()
        .AddExceptionHandler<GlobalExceptionHandler>();

    builder.Services.AddOptions<ExpenseOptions>()
        .Bind(builder.Configuration.GetSection(ExpenseOptions.SectionName))
        .ValidateDataAnnotations()
        .ValidateOnStart();
    builder.Services.AddOptions<AppOptions>()
        .Bind(builder.Configuration.GetSection(AppOptions.SectionName))
        .ValidateDataAnnotations()
        .ValidateOnStart();

    // Behind a TLS-terminating proxy (Render, Fly, a load balancer) the real scheme and client IP
    // arrive in X-Forwarded-* headers. Without this, every client shares the proxy's IP for rate
    // limiting and the app believes requests are plain HTTP. Only the last hop (the proxy itself)
    // is trusted, so a client cannot spoof its IP by sending its own X-Forwarded-For.
    var behindProxy = builder.Configuration.GetValue<bool>("ForwardedHeaders:Enabled");
    if (behindProxy)
    {
        builder.Services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            o.ForwardLimit = 1;
            o.KnownIPNetworks.Clear(); // the platform's proxy addresses are not known in advance
            o.KnownProxies.Clear();
        });
    }

    var app = builder.Build();

    if (behindProxy)
    {
        app.UseForwardedHeaders();
    }

    app.UseExceptionHandler();
    app.UseSpendScopeRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.UseWebAssemblyDebugging();
        app.MapOpenApi();
    }
    else
    {
        app.UseHsts();
    }

    app.UseHttpsRedirection();

    // Hosted Blazor WebAssembly: the SpendScope.Web bundle is served from this origin (spec section 19.1).
    app.UseBlazorFrameworkFiles();
    app.UseStaticFiles();

    app.UseAuthentication();
    app.UseRateLimiter();
    app.UseAuthorization();

    app.MapSpendScopeHealthChecks();
    app.MapFeatureEndpoints(typeof(IFeatureEndpoint).Assembly);

    // Unknown /api routes are 404 JSON, not the Blazor index page.
    app.MapFallback("/api/{**path}", () => Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Not found."));
    app.MapFallbackToFile("index.html");

    await app.Services.InitializeDatabaseAsync(app.Configuration, seedDevelopmentAdmin: app.Environment.IsDevelopment());

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    // HostAbortedException is thrown on purpose by EF Core tooling and WebApplicationFactory.
    Log.Fatal(ex, "SpendScope.Api terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

/// <summary>Exposed so integration tests can use <c>WebApplicationFactory&lt;Program&gt;</c>.</summary>
public partial class Program;
