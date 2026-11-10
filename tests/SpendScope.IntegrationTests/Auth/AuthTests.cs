using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SpendScope.Contracts.Auth;
using SpendScope.IntegrationTests.Infrastructure;

namespace SpendScope.IntegrationTests.Auth;

/// <summary>Spec 15.1 to 15.7 and 13.5 against the real Api and PostgreSQL.</summary>
[Collection(ApiTestGroup.Name)]
public sealed class AuthTests(ApiFixture api)
{
    [Fact]
    public async Task Register_creates_a_User_and_rejects_the_same_email_in_any_case()
    {
        var email = ApiFixture.UniqueEmail("priya");
        using var client = api.Anonymous();

        var created = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, ApiFixture.Password, "Priya Sharma"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("/api/me", created.Headers.Location?.ToString());

        var duplicate = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email.ToUpperInvariant(), ApiFixture.Password, "Again"));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        using var user = await api.LoginAsync(email);
        var me = await user.Client.GetFromJsonAsync<UserProfileDto>("/api/me");
        Assert.Equal(["User"], me!.Roles);
        Assert.Equal("USD", me.DefaultCurrency);
    }

    [Fact]
    public async Task Register_reports_every_invalid_field()
    {
        using var client = api.Anonymous();
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("nope", "weak", "P"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("email_invalid", await response.ErrorCodeAsync("email"));
        Assert.Equal("password_policy", await response.ErrorCodeAsync("password"));
        Assert.Equal("display_name_length", await response.ErrorCodeAsync("displayName"));
    }

    [Fact]
    public async Task Wrong_password_and_unknown_email_get_the_same_answer()
    {
        using var user = await api.NewUserAsync();
        using var client = api.Anonymous();

        var wrong = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(user.Email, "Wr0ng!Password"));
        var unknown = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(ApiFixture.UniqueEmail("ghost"), "Wr0ng!Password"));

        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal(await wrong.ErrorCodeAsync(), await unknown.ErrorCodeAsync());
    }

    [Fact]
    public async Task Five_failed_logins_lock_the_account_with_Retry_After()
    {
        using var user = await api.NewUserAsync();
        using var client = api.Anonymous();

        HttpResponseMessage last = null!;
        for (var i = 0; i < 5; i++)
        {
            last = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(user.Email, "Wr0ng!Password"));
        }

        Assert.Equal((HttpStatusCode)423, last.StatusCode);
        Assert.NotNull(last.Headers.RetryAfter?.Delta);

        // Even the right password is refused while locked.
        var correct = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(user.Email, ApiFixture.Password));
        Assert.Equal((HttpStatusCode)423, correct.StatusCode);
    }

    [Fact]
    public async Task Refresh_rotates_and_reuse_revokes_every_session()
    {
        using var user = await api.NewUserAsync();
        using var client = api.Anonymous();

        var first = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(user.Tokens.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var rotated = (await first.Content.ReadFromJsonAsync<TokenResponse>())!;
        Assert.NotEqual(user.Tokens.RefreshToken, rotated.RefreshToken);

        // Presenting the old token again looks like theft (spec 13.5).
        var reuse = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(user.Tokens.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
        Assert.Equal("refresh_token_reused", await reuse.ErrorCodeAsync());

        // ...and the legitimately rotated token is now dead too.
        var afterReuse = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(rotated.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, afterReuse.StatusCode);
    }

    [Fact]
    public async Task Concurrent_refreshes_with_one_token_cannot_both_win()
    {
        using var user = await api.NewUserAsync();
        using var a = api.Anonymous();
        using var b = api.Anonymous();

        var results = await Task.WhenAll(
            a.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(user.Tokens.RefreshToken)),
            b.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(user.Tokens.RefreshToken)));

        Assert.True(results.Count(r => r.StatusCode == HttpStatusCode.OK) <= 1, "at most one refresh may succeed");
    }

    [Fact]
    public async Task Logout_revokes_the_refresh_token_and_is_idempotent()
    {
        using var user = await api.NewUserAsync();

        Assert.Equal(HttpStatusCode.NoContent, (await user.Client.PostAsJsonAsync("/api/auth/logout", new LogoutRequest(user.Tokens.RefreshToken))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await user.Client.PostAsJsonAsync("/api/auth/logout", new LogoutRequest(user.Tokens.RefreshToken))).StatusCode);

        using var anon = api.Anonymous();
        var refresh = await anon.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(user.Tokens.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Change_password_checks_the_current_one_and_ends_other_sessions()
    {
        using var user = await api.NewUserAsync();

        var wrong = await user.Client.PostAsJsonAsync("/api/auth/change-password", new ChangePasswordRequest("Wr0ng!Password", "N3w!Passphrase"));
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.Equal("password_incorrect", await wrong.ErrorCodeAsync("currentPassword"));

        var changed = await user.Client.PostAsJsonAsync("/api/auth/change-password", new ChangePasswordRequest(ApiFixture.Password, "N3w!Passphrase"));
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);

        using var anon = api.Anonymous();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(user.Tokens.RefreshToken))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anon.PostAsJsonAsync("/api/auth/login", new LoginRequest(user.Email, "N3w!Passphrase"))).StatusCode);
    }

    [Fact]
    public async Task Forgot_password_response_is_identical_for_known_and_unknown_emails()
    {
        using var user = await api.NewUserAsync();
        using var client = api.Anonymous();

        var known = await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest(user.Email));
        var unknown = await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest(ApiFixture.UniqueEmail("ghost")));

        Assert.Equal(HttpStatusCode.Accepted, known.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, unknown.StatusCode);
        Assert.Equal(await known.Content.ReadAsStringAsync(), await unknown.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Profile_update_persists_and_rejects_unsupported_currency()
    {
        using var user = await api.NewUserAsync();

        var bad = await user.Client.PutAsJsonAsync("/api/me", new UpdateProfileRequest("Priya", "BTC"));
        Assert.Equal("currency_unsupported", await bad.ErrorCodeAsync("defaultCurrency"));

        var ok = await user.Client.PutAsJsonAsync("/api/me", new UpdateProfileRequest("Priya S.", "INR"));
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var me = await user.Client.GetFromJsonAsync<UserProfileDto>("/api/me");
        Assert.Equal(("Priya S.", "INR"), (me!.DisplayName, me.DefaultCurrency));
    }

    [Theory]
    [InlineData("/api/me")]
    [InlineData("/api/expenses")]
    [InlineData("/api/categories")]
    [InlineData("/api/reports/summary")]
    public async Task Protected_endpoints_require_a_token(string url)
    {
        using var client = api.Anonymous();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(new Uri(url, UriKind.Relative))).StatusCode);
    }

    [Fact]
    public async Task Tampered_token_is_rejected()
    {
        using var user = await api.NewUserAsync();
        using var client = api.Anonymous();
        var parts = user.Tokens.AccessToken.Split('.');
        var forged = $"{parts[0]}.{parts[1]}.{new string('A', parts[2].Length)}";
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", forged);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(new Uri("/api/me", UriKind.Relative))).StatusCode);
    }
}
