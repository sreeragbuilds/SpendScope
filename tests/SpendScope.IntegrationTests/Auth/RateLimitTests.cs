using System.Net;
using System.Net.Http.Json;
using SpendScope.Contracts.Auth;
using SpendScope.IntegrationTests.Infrastructure;

namespace SpendScope.IntegrationTests.Auth;

/// <summary>Spec 14.5: auth endpoints are throttled per IP and answer 429 with Retry-After.</summary>
[Collection(ApiTestGroup.Name)]
public sealed class RateLimitTests(ApiFixture api)
{
    [Fact]
    public async Task Login_is_throttled_after_the_configured_limit()
    {
        await using var factory = new SpendScopeApiFactory(
            api.ConnectionString,
            settings: new Dictionary<string, string> { ["RateLimiting:AuthStrictPerMinute"] = "3" });
        using var client = factory.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(ApiFixture.UniqueEmail("rl"), "Wr0ng!Password"));
            statuses.Add(response.StatusCode);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                Assert.NotNull(response.Headers.RetryAfter);
                Assert.Equal("rate_limited", await response.ErrorCodeAsync());
            }
        }

        Assert.Equal([HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests], statuses);
    }
}
