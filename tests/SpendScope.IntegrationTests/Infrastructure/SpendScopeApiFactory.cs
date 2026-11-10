using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SpendScope.IntegrationTests.Infrastructure;

/// <summary>
/// Hosts the real Api in memory. Runs in the "Testing" environment so developer user secrets
/// and Development-only middleware are never picked up.
/// </summary>
public sealed class SpendScopeApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    /// <summary>Nothing listens on port 1, so connections are refused immediately.</summary>
    public const string UnreachableDatabase =
        "Host=127.0.0.1;Port=1;Database=spendscope;Username=none;Password=none;Timeout=2";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", connectionString);
    }
}
