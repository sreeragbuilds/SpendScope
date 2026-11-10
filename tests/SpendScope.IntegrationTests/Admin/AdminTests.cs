using System.Net;
using System.Net.Http.Json;
using SpendScope.Contracts.Admin;
using SpendScope.Contracts.Auth;
using SpendScope.Contracts.Common;
using SpendScope.Contracts.Expenses;
using SpendScope.Domain.Categories;
using SpendScope.IntegrationTests.Infrastructure;

namespace SpendScope.IntegrationTests.Admin;

/// <summary>Spec 15.20 to 15.22. FR-ADM-01 to 05.</summary>
[Collection(ApiTestGroup.Name)]
public sealed class AdminTests(ApiFixture api)
{
    [Theory]
    [InlineData("/api/admin/users")]
    [InlineData("/api/admin/reports/category-totals")]
    [InlineData("/api/admin/reports/monthly")]
    public async Task Regular_users_get_403(string url)
    {
        using var user = await api.NewUserAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await user.Client.GetAsync(new Uri(url, UriKind.Relative))).StatusCode);
    }

    [Fact]
    public async Task Admin_lists_and_searches_users_without_expense_data()
    {
        using var admin = await api.NewAdminAsync();
        using var target = await api.NewUserAsync("findme");
        var prefix = target.Email[..20];

        var response = await admin.Client.GetAsync(new Uri($"/api/admin/users?search={Uri.EscapeDataString(prefix)}", UriKind.Relative));
        var raw = await response.Content.ReadAsStringAsync();
        var page = (await response.Content.ReadFromJsonAsync<PagedResponse<AdminUserDto>>())!;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var found = Assert.Single(page.Items);
        Assert.Equal((target.Email, false), (found.Email, found.IsLocked));
        Assert.DoesNotContain("amount", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Lock_blocks_login_and_refresh_and_unlock_restores_login()
    {
        using var admin = await api.NewAdminAsync();
        using var target = await api.NewUserAsync();
        using var anon = api.Anonymous();

        Assert.Equal(HttpStatusCode.NoContent, (await admin.Client.PostAsync(new Uri($"/api/admin/users/{target.Id}/lock", UriKind.Relative), null)).StatusCode);

        Assert.Equal((HttpStatusCode)423, (await anon.PostAsJsonAsync("/api/auth/login", new LoginRequest(target.Email, ApiFixture.Password))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(target.Tokens.RefreshToken))).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.Client.PostAsync(new Uri($"/api/admin/users/{target.Id}/unlock", UriKind.Relative), null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anon.PostAsJsonAsync("/api/auth/login", new LoginRequest(target.Email, ApiFixture.Password))).StatusCode);
    }

    [Fact]
    public async Task Admin_cannot_lock_themselves_and_unknown_users_are_404()
    {
        using var admin = await api.NewAdminAsync();

        var self = await admin.Client.PostAsync(new Uri($"/api/admin/users/{admin.Id}/lock", UriKind.Relative), null);
        Assert.Equal(HttpStatusCode.BadRequest, self.StatusCode);
        Assert.Equal("cannot_lock_self", await self.ErrorCodeAsync("id"));

        Assert.Equal(HttpStatusCode.NotFound, (await admin.Client.PostAsync(new Uri($"/api/admin/users/{Guid.NewGuid()}/lock", UriKind.Relative), null)).StatusCode);
    }

    [Fact]
    public async Task System_totals_aggregate_everyone_and_never_expose_user_ids_or_custom_names()
    {
        using var admin = await api.NewAdminAsync();
        using var user = await api.NewUserAsync();
        var custom = (await (await user.Client.PostAsJsonAsync("/api/categories",
            new Contracts.Categories.CreateCategoryRequest("Very private name", "#123456"))).Content.ReadFromJsonAsync<Contracts.Categories.CategoryDto>())!;
        var day = new DateOnly(2001, 2, 3); // a date no other test uses
        await user.Client.PostAsJsonAsync("/api/expenses", new CreateExpenseRequest(10m, day, SystemCategories.Health, null));
        await user.Client.PostAsJsonAsync("/api/expenses", new CreateExpenseRequest(30m, day, custom.Id, null));

        var response = await admin.Client.GetAsync(new Uri("/api/admin/reports/category-totals?from=2001-02-03&to=2001-02-03", UriKind.Relative));
        var raw = await response.Content.ReadAsStringAsync();
        var totals = (await response.Content.ReadFromJsonAsync<SystemCategoryTotalsDto>())!;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("userId", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Very private name", raw, StringComparison.Ordinal);
        Assert.Equal(40m, totals.GrandTotal);
        Assert.Equal(["Custom categories", "Health"], totals.Categories.Select(c => c.Name));

        // The admin's own expense endpoints still show only the admin's own rows.
        var adminExpenses = await admin.Client.GetFromJsonAsync<PagedResponse<ExpenseDto>>("/api/expenses?from=2001-02-01&to=2001-02-28");
        Assert.Equal(0, adminExpenses!.TotalCount);
    }
}
