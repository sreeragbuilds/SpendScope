using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SpendScope.Application.Common;
using SpendScope.Contracts.Auth;
using SpendScope.Infrastructure.Identity;
using Testcontainers.PostgreSql;

namespace SpendScope.IntegrationTests.Infrastructure;

/// <summary>
/// One PostgreSQL 16 container and one migrated Api for every test in the "Api" collection.
/// Tests isolate themselves by registering their own users with unique emails.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    public const string Password = "Str0ng!Passphrase";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16")
        .WithDatabase("spendscope")
        .WithUsername("spendscope")
        .WithPassword("integration-tests")
        .Build();

    public SpendScopeApiFactory Factory { get; private set; } = null!;

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        Factory = new SpendScopeApiFactory(ConnectionString, migrate: true);
        _ = Factory.Server; // start the host now: applies migrations once for the collection
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _container.DisposeAsync();
    }

    public HttpClient Anonymous() => Factory.CreateClient();

    public static string UniqueEmail(string who) => $"{who}.{Guid.NewGuid():N}@example.com";

    /// <summary>Registers a new user and returns a client that sends their access token.</summary>
    public async Task<TestUser> NewUserAsync(string who = "user")
    {
        var email = UniqueEmail(who);
        using var anon = Anonymous();
        (await anon.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, Password, who))).EnsureSuccessStatusCode();
        return await LoginAsync(email);
    }

    /// <summary>Creates a user with the Admin role directly through Identity (there is no API for that).</summary>
    public async Task<TestUser> NewAdminAsync()
    {
        var user = await NewUserAsync("admin");
        using (var scope = Factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var entity = await users.FindByEmailAsync(user.Email);
            (await users.AddToRoleAsync(entity!, Roles.Admin)).Succeeded.ShouldBeTrue();
        }

        // Log in again so the access token carries the new role.
        return await LoginAsync(user.Email);
    }

    public async Task<TestUser> LoginAsync(string email)
    {
        using var anon = Anonymous();
        var response = await anon.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, Password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tokens = (await response.Content.ReadFromJsonAsync<TokenResponse>())!;

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        var me = (await client.GetFromJsonAsync<UserProfileDto>("/api/me"))!;
        return new TestUser(me.Id, email, client, tokens);
    }
}

public sealed record TestUser(Guid Id, string Email, HttpClient Client, TokenResponse Tokens) : IDisposable
{
    public void Dispose() => Client.Dispose();
}

[CollectionDefinition(Name)]
public sealed class ApiTestGroup : ICollectionFixture<ApiFixture>
{
    public const string Name = "Api";
}

internal static class TestExtensions
{
    public static void ShouldBeTrue(this bool value) => Assert.True(value);

    // ReadAsStringAsync reads the buffered body, so several assertions can inspect one response.
    public static async Task<JsonDocument> JsonAsync(this HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    /// <summary>The <c>errorCode</c> of a problem response, or the first per-field code of a validation problem.</summary>
    public static async Task<string?> ErrorCodeAsync(this HttpResponseMessage response, string? field = null)
    {
        using var json = await response.JsonAsync();
        if (field is not null)
        {
            return json.RootElement.TryGetProperty("errorCodes", out var codes) && codes.TryGetProperty(field, out var list)
                ? list[0].GetString()
                : null;
        }

        return json.RootElement.TryGetProperty("errorCode", out var code) ? code.GetString() : null;
    }
}
