using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SpendScope.IntegrationTests.Health;

/// <summary>Spec section 22.3: the app refuses to start with invalid configuration.</summary>
public sealed class StartupConfigurationTests
{
    [Fact]
    public void Startup_fails_fast_when_the_connection_string_is_missing()
    {
        using var factory = new MissingConnectionStringFactory();

        var ex = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("ConnectionStrings:Default", ex.Message, StringComparison.Ordinal);
    }

    private sealed class MissingConnectionStringFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) =>
            builder.UseEnvironment("Testing");
    }
}
