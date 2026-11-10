using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SpendScope.IntegrationTests.Infrastructure;

/// <summary>
/// Hosts the real Api in memory. Runs in the "Testing" environment so developer user secrets
/// and Development-only middleware are never picked up.
/// </summary>
public sealed class SpendScopeApiFactory(string connectionString, bool migrate = false, IReadOnlyDictionary<string, string>? settings = null)
    : WebApplicationFactory<Program>
{
    /// <summary>Nothing listens on port 1, so connections are refused immediately.</summary>
    public const string UnreachableDatabase =
        "Host=127.0.0.1;Port=1;Database=spendscope;Username=none;Password=none;Timeout=2";

    /// <summary>A throwaway key for tests only; real keys come from secrets.</summary>
    public const string TestSigningKey = "integration-tests-signing-key-0123456789abcdef";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", connectionString);
        builder.UseSetting("Jwt:SigningKey", TestSigningKey);
        builder.UseSetting("Database:MigrateOnStartup", migrate ? "true" : "false");
        builder.UseSetting("App:PublicBaseUrl", "https://spendscope.test");

        // Generous by default so tests are not throttled; RateLimitTests lowers them.
        builder.UseSetting("RateLimiting:AuthStrictPerMinute", "10000");
        builder.UseSetting("RateLimiting:AuthRefreshPerMinute", "10000");
        builder.UseSetting("RateLimiting:ApiPerMinute", "10000");

        foreach (var (key, value) in settings ?? new Dictionary<string, string>())
        {
            builder.UseSetting(key, value);
        }
    }
}
