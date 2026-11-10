using System.Net;
using System.Text.Json;
using SpendScope.IntegrationTests.Infrastructure;

namespace SpendScope.IntegrationTests.Health;

/// <summary>Spec section 20.4. Milestone 0 demo: /health/ready returns healthy.</summary>
[Collection(PostgresTestGroup.Name)]
public sealed class HealthEndpointTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Ready_is_healthy_when_PostgreSQL_is_reachable()
    {
        await using var factory = new SpendScopeApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await ReadJsonAsync(response);
        Assert.Equal("Healthy", body.RootElement.GetProperty("status").GetString());
        var check = Assert.Single(body.RootElement.GetProperty("checks").EnumerateArray());
        Assert.Equal("postgres", check.GetProperty("name").GetString());
        Assert.Equal("Healthy", check.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Ready_returns_503_when_PostgreSQL_is_unreachable()
    {
        await using var factory = new SpendScopeApiFactory(SpendScopeApiFactory.UnreachableDatabase);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        using var body = await ReadJsonAsync(response);
        Assert.Equal("Unhealthy", body.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Live_is_healthy_even_when_PostgreSQL_is_unreachable()
    {
        // A database outage must not make the orchestrator restart a working process.
        await using var factory = new SpendScopeApiFactory(SpendScopeApiFactory.UnreachableDatabase);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health/live", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await ReadJsonAsync(response);
        Assert.Equal("Healthy", body.RootElement.GetProperty("status").GetString());
        Assert.Empty(body.RootElement.GetProperty("checks").EnumerateArray());
    }

    [Fact]
    public async Task Health_responses_do_not_leak_exception_details()
    {
        // The endpoints are anonymous; connection errors belong in logs, not responses.
        await using var factory = new SpendScopeApiFactory(SpendScopeApiFactory.UnreachableDatabase);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative));
        var raw = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("exception", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Host=", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("127.0.0.1", raw, StringComparison.Ordinal);
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        await using var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }
}
