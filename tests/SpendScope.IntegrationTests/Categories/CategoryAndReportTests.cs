using System.Net;
using System.Net.Http.Json;
using SpendScope.Contracts.Categories;
using SpendScope.Contracts.Common;
using SpendScope.Contracts.Expenses;
using SpendScope.Contracts.Reports;
using SpendScope.Domain.Categories;
using SpendScope.IntegrationTests.Infrastructure;

namespace SpendScope.IntegrationTests.Categories;

/// <summary>Spec 15.14 to 15.17 and section 16.</summary>
[Collection(ApiTestGroup.Name)]
public sealed class CategoryAndReportTests(ApiFixture api)
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private static async Task<CategoryDto> CreateCategoryAsync(TestUser user, string name)
    {
        var response = await user.Client.PostAsJsonAsync("/api/categories", new CreateCategoryRequest(name, "#7C2D12"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CategoryDto>())!;
    }

    private static async Task AddExpenseAsync(TestUser user, decimal amount, Guid category, DateOnly? on = null) =>
        Assert.Equal(HttpStatusCode.Created,
            (await user.Client.PostAsJsonAsync("/api/expenses", new CreateExpenseRequest(amount, on ?? Today, category, null))).StatusCode);

    [Fact]
    public async Task List_returns_system_categories_first_then_own()
    {
        using var user = await api.NewUserAsync();
        await CreateCategoryAsync(user, "Coffee");

        var categories = (await user.Client.GetFromJsonAsync<List<CategoryDto>>("/api/categories"))!;

        Assert.Equal(11, categories.Count);
        Assert.All(categories.Take(10), c => Assert.True(c.IsSystem));
        Assert.Equal("Coffee", categories[10].Name);
    }

    [Theory]
    [InlineData("groceries")]
    [InlineData("GROCERIES")]
    public async Task Names_must_be_unique_against_system_and_own_ignoring_case(string name)
    {
        using var user = await api.NewUserAsync();
        await CreateCategoryAsync(user, "Coffee");

        Assert.Equal(HttpStatusCode.Conflict, (await user.Client.PostAsJsonAsync("/api/categories", new CreateCategoryRequest(name, "#123456"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await user.Client.PostAsJsonAsync("/api/categories", new CreateCategoryRequest("coffee", "#123456"))).StatusCode);
    }

    [Fact]
    public async Task System_categories_are_read_only_for_everyone()
    {
        using var admin = await api.NewAdminAsync();

        var update = await admin.Client.PutAsJsonAsync($"/api/categories/{SystemCategories.Groceries}", new UpdateCategoryRequest("Food", "#000000"));
        var delete = await admin.Client.DeleteAsync(new Uri($"/api/categories/{SystemCategories.Groceries}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
        Assert.Equal(ErrorCodes.SystemCategoryReadOnly, await update.ErrorCodeAsync());
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task Rename_to_an_existing_name_is_a_conflict()
    {
        using var user = await api.NewUserAsync();
        await CreateCategoryAsync(user, "Coffee");
        var tea = await CreateCategoryAsync(user, "Tea");

        var response = await user.Client.PutAsJsonAsync($"/api/categories/{tea.Id}", new UpdateCategoryRequest("COFFEE", "#000000"));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Delete_requires_a_replacement_when_in_use_and_moves_every_expense()
    {
        using var user = await api.NewUserAsync();
        var coffee = await CreateCategoryAsync(user, "Coffee");
        await AddExpenseAsync(user, 3.5m, coffee.Id);
        await AddExpenseAsync(user, 4.5m, coffee.Id);

        var refused = await user.Client.DeleteAsync(new Uri($"/api/categories/{coffee.Id}", UriKind.Relative));
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal(ErrorCodes.ReplacementRequired, await refused.ErrorCodeAsync("replacementId"));

        var deleted = await user.Client.DeleteAsync(new Uri($"/api/categories/{coffee.Id}?replacementId={SystemCategories.FoodAndDining}", UriKind.Relative));
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var expenses = await user.Client.GetFromJsonAsync<PagedResponse<ExpenseDto>>("/api/expenses");
        Assert.Equal(2, expenses!.TotalCount);
        Assert.All(expenses.Items, e => Assert.Equal(SystemCategories.FoodAndDining, e.Category.Id));
    }

    [Fact]
    public async Task Unused_category_deletes_without_a_replacement()
    {
        using var user = await api.NewUserAsync();
        var unused = await CreateCategoryAsync(user, "Unused");

        Assert.Equal(HttpStatusCode.NoContent, (await user.Client.DeleteAsync(new Uri($"/api/categories/{unused.Id}", UriKind.Relative))).StatusCode);
    }

    [Fact]
    public async Task Summary_aggregates_by_category_with_percentages()
    {
        using var user = await api.NewUserAsync();
        using var noise = await api.NewUserAsync();
        await AddExpenseAsync(user, 75m, SystemCategories.Housing);
        await AddExpenseAsync(user, 20m, SystemCategories.Groceries);
        await AddExpenseAsync(user, 5m, SystemCategories.Groceries);
        await AddExpenseAsync(noise, 1000m, SystemCategories.Housing);

        var summary = (await user.Client.GetFromJsonAsync<CategorySummaryDto>($"/api/reports/summary?from={Today:yyyy-MM-dd}&to={Today:yyyy-MM-dd}"))!;

        Assert.Equal((100m, 3, 33.33m, "USD"), (summary.GrandTotal, summary.ExpenseCount, summary.AverageExpense, summary.Currency));
        Assert.Equal(["Housing", "Groceries"], summary.Categories.Select(c => c.Name));
        Assert.Equal([75m, 25m], summary.Categories.Select(c => c.Percentage));
        Assert.Equal([1, 2], summary.Categories.Select(c => c.Count));
    }

    [Fact]
    public async Task Summary_for_an_empty_range_is_zero_not_an_error()
    {
        using var user = await api.NewUserAsync();
        var summary = (await user.Client.GetFromJsonAsync<CategorySummaryDto>("/api/reports/summary?from=2020-01-01&to=2020-01-31"))!;

        Assert.Equal((0m, 0, 0m), (summary.GrandTotal, summary.ExpenseCount, summary.AverageExpense));
        Assert.Empty(summary.Categories);
    }

    [Fact]
    public async Task Summary_rejects_ranges_over_366_days()
    {
        using var user = await api.NewUserAsync();
        var response = await user.Client.GetAsync(new Uri("/api/reports/summary?from=2024-01-01&to=2025-06-30", UriKind.Relative));
        Assert.Equal("range_too_large", await response.ErrorCodeAsync("to"));
    }

    [Fact]
    public async Task Monthly_returns_twelve_zero_filled_months()
    {
        using var user = await api.NewUserAsync();
        var year = Today.Year;
        await AddExpenseAsync(user, 10m, SystemCategories.Other, new DateOnly(year, 1, 15));
        await AddExpenseAsync(user, 15m, SystemCategories.Other, new DateOnly(year, 1, 20));

        var monthly = (await user.Client.GetFromJsonAsync<MonthlyTotalsDto>($"/api/reports/monthly?year={year}"))!;

        Assert.Equal(12, monthly.Months.Count);
        Assert.Equal((25m, 2), (monthly.Months[0].Total, monthly.Months[0].Count));
        Assert.Equal(0m, monthly.Months.Skip(1).Where(m => m.Month != Today.Month).Sum(m => m.Total));
    }
}
