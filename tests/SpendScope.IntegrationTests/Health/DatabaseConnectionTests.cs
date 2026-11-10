using Microsoft.Extensions.Configuration;
using Npgsql;
using SpendScope.Infrastructure.Persistence;

namespace SpendScope.IntegrationTests.Health;

/// <summary>DATABASE_URL support for platforms like Render (no database needed).</summary>
public sealed class DatabaseConnectionTests
{
    [Fact]
    public void Converts_a_platform_url_including_encoded_credentials_and_sslmode()
    {
        var cs = new NpgsqlConnectionStringBuilder(
            DatabaseConnection.FromUrl("postgresql://spend%40user:p%40ss%3Aword@dpg-abc.singapore-postgres.render.com:6543/spendscope_db?sslmode=require"));

        Assert.Equal("dpg-abc.singapore-postgres.render.com", cs.Host);
        Assert.Equal(6543, cs.Port);
        Assert.Equal("spendscope_db", cs.Database);
        Assert.Equal("spend@user", cs.Username);
        Assert.Equal("p@ss:word", cs.Password);
        Assert.Equal(SslMode.Require, cs.SslMode);
    }

    [Fact]
    public void Defaults_to_port_5432()
    {
        var cs = new NpgsqlConnectionStringBuilder(DatabaseConnection.FromUrl("postgres://u:p@dpg-internal/db"));
        Assert.Equal(5432, cs.Port);
    }

    [Fact]
    public void ConnectionStrings_Default_wins_over_DATABASE_URL()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = "Host=explicit;Database=a",
            ["DATABASE_URL"] = "postgresql://u:p@from-url/db",
        }).Build();

        Assert.Equal("Host=explicit;Database=a", DatabaseConnection.Resolve(config));
    }

    [Theory]
    [InlineData("mysql://u:p@host/db")]
    [InlineData("not a url")]
    public void Rejects_non_postgres_urls(string url) =>
        Assert.Throws<InvalidOperationException>(() => DatabaseConnection.FromUrl(url));
}
