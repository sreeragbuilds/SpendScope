using Serilog;
using SpendScope.Api.Endpoints;
using SpendScope.Api.HealthChecks;
using SpendScope.Api.Logging;
using SpendScope.Features;

// Startup logger: catches failures before the host's logger is built (bad config, missing secrets).
// AddSerilog replaces it once the host starts.
Log.Logger = new LoggerConfiguration().WriteTo.Console(formatProvider: null).CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.AddSpendScopeLogging();
    builder.Services.AddSpendScopeHealthChecks(builder.Configuration);

    var app = builder.Build();

    app.UseSpendScopeRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.UseWebAssemblyDebugging();
    }
    else
    {
        app.UseHsts();
    }

    app.UseHttpsRedirection();

    // Hosted Blazor WebAssembly: the SpendScope.Web bundle is served from this origin (spec section 19.1).
    app.UseBlazorFrameworkFiles();
    app.UseStaticFiles();

    app.MapSpendScopeHealthChecks();
    app.MapFeatureEndpoints(typeof(IFeatureEndpoint).Assembly);
    app.MapFallbackToFile("index.html");

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
