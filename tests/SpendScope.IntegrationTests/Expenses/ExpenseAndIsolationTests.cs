using System.Net;
using System.Net.Http.Json;
using SpendScope.Contracts.Categories;
using SpendScope.Contracts.Common;
using SpendScope.Contracts.Expenses;
using SpendScope.Domain.Categories;
using SpendScope.IntegrationTests.Infrastructure;

namespace SpendScope.IntegrationTests.Expenses;

/// <summary>Expense CRUD (spec 15.8 to 15.13) and per-user isolation (FR-EXP-10, IDOR).</summary>
[Collection(ApiTestGroup.Name)]
public sealed class ExpenseAndIsolationTests(ApiFixture api)
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private static async Task<ExpenseDto> CreateAsync(TestUser user, decimal amount, Guid? category = null, DateOnly? on = null, string? note = null)
    {
        var response = await user.Client.PostAsJsonAsync("/api/expenses", new CreateExpenseRequest(amount, on ?? Today, category ?? SystemCategories.Groceries, note));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ExpenseDto>())!;
    }

    [Fact]
    public async Task Create_returns_201_with_location_and_the_users_currency()
    {
        using var user = await api.NewUserAsync();

        var response = await user.Client.PostAsJsonAsync("/api/expenses", new CreateExpenseRequest(42.50m, Today, SystemCategories.FoodAndDining, "Lunch with team"));
        var dto = (await response.Content.ReadFromJsonAsync<ExpenseDto>())!;

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"/api/expenses/{dto.Id}", response.Headers.Location?.ToString());
        Assert.Equal(("USD", "Food and Dining", 42.50m), (dto.Currency, dto.Category.Name, dto.Amount));
    }

    [Fact]
    public async Task Invalid_expenses_are_rejected_with_field_codes()
    {
        using var user = await api.NewUserAsync();

        var response = await user.Client.PostAsJsonAsync("/api/expenses", new CreateExpenseRequest(0, Today.AddDays(400), Guid.Empty, new string('n', 501)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("amount_range", await response.ErrorCodeAsync("amount"));
        Assert.Equal("date_future", await response.ErrorCodeAsync("occurredOn"));
        Assert.Equal("category_required", await response.ErrorCodeAsync("categoryId"));
        Assert.Equal("note_length", await response.ErrorCodeAsync("note"));
    }

    [Fact]
    public async Task Update_changes_fields_and_sets_UpdatedAtUtc()
    {
        using var user = await api.NewUserAsync();
        var expense = await CreateAsync(user, 10);

        var response = await user.Client.PutAsJsonAsync($"/api/expenses/{expense.Id}", new UpdateExpenseRequest(12.34m, Today.AddDays(-1), SystemCategories.Transport, "taxi"));
        var updated = (await response.Content.ReadFromJsonAsync<ExpenseDto>())!;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal((12.34m, "Transport", "taxi"), (updated.Amount, updated.Category.Name, updated.Note));
        Assert.NotNull(updated.UpdatedAtUtc);
    }

    [Fact]
    public async Task Another_users_expense_is_invisible_for_read_update_and_delete()
    {
        using var owner = await api.NewUserAsync("owner");
        using var intruder = await api.NewUserAsync("intruder");
        var expense = await CreateAsync(owner, 99.99m, note: "private");

        Assert.Equal(HttpStatusCode.NotFound, (await intruder.Client.GetAsync(new Uri($"/api/expenses/{expense.Id}", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await intruder.Client.PutAsJsonAsync($"/api/expenses/{expense.Id}", new UpdateExpenseRequest(1, Today, SystemCategories.Other, "hacked"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.Client.DeleteAsync(new Uri($"/api/expenses/{expense.Id}", UriKind.Relative))).StatusCode);

        // The owner's expense is untouched.
        var still = await owner.Client.GetFromJsonAsync<ExpenseDto>($"/api/expenses/{expense.Id}");
        Assert.Equal((99.99m, "private"), (still!.Amount, still.Note));

        var intruderList = await intruder.Client.GetFromJsonAsync<PagedResponse<ExpenseDto>>("/api/expenses");
        Assert.Equal(0, intruderList!.TotalCount);
    }

    [Fact]
    public async Task Another_users_category_cannot_be_used_or_seen()
    {
        using var owner = await api.NewUserAsync("owner");
        using var intruder = await api.NewUserAsync("intruder");
        var category = (await (await owner.Client.PostAsJsonAsync("/api/categories", new CreateCategoryRequest("Secret hobby", "#123456"))).Content.ReadFromJsonAsync<CategoryDto>())!;

        var create = await intruder.Client.PostAsJsonAsync("/api/expenses", new CreateExpenseRequest(5, Today, category.Id, null));
        Assert.Equal("category_not_found", await create.ErrorCodeAsync("categoryId"));

        var visible = await intruder.Client.GetFromJsonAsync<List<CategoryDto>>("/api/categories");
        Assert.DoesNotContain(visible!, c => c.Id == category.Id);
        Assert.Equal(HttpStatusCode.NotFound,
            (await intruder.Client.PutAsJsonAsync($"/api/categories/{category.Id}", new UpdateCategoryRequest("Mine now", "#000000"))).StatusCode);
    }

    [Fact]
    public async Task Delete_is_hard_and_a_second_delete_is_404()
    {
        using var user = await api.NewUserAsync();
        var expense = await CreateAsync(user, 3);

        Assert.Equal(HttpStatusCode.NoContent, (await user.Client.DeleteAsync(new Uri($"/api/expenses/{expense.Id}", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await user.Client.DeleteAsync(new Uri($"/api/expenses/{expense.Id}", UriKind.Relative))).StatusCode);
    }

    [Fact]
    public async Task List_filters_sorts_and_pages()
    {
        using var user = await api.NewUserAsync();
        await CreateAsync(user, 5, SystemCategories.Groceries);
        await CreateAsync(user, 50, SystemCategories.Housing);
        await CreateAsync(user, 20, SystemCategories.Groceries);
        await CreateAsync(user, 7, SystemCategories.Groceries, Today.AddMonths(-3));

        var thisMonth = await user.Client.GetFromJsonAsync<PagedResponse<ExpenseDto>>(
            $"/api/expenses?from={Today.AddDays(-7):yyyy-MM-dd}&to={Today:yyyy-MM-dd}&sortBy=amount&sortDir=asc&pageSize=2");
        Assert.Equal(3, thisMonth!.TotalCount);
        Assert.Equal(2, thisMonth.TotalPages);
        Assert.Equal([5m, 20m], thisMonth.Items.Select(e => e.Amount));

        var groceriesOver10 = await user.Client.GetFromJsonAsync<PagedResponse<ExpenseDto>>(
            $"/api/expenses?from={Today.AddDays(-7):yyyy-MM-dd}&to={Today:yyyy-MM-dd}&categoryId={SystemCategories.Groceries}&minAmount=10");
        Assert.Equal([20m], groceriesOver10!.Items.Select(e => e.Amount));
    }

    [Fact]
    public async Task List_rejects_unknown_sort_and_oversized_page()
    {
        using var user = await api.NewUserAsync();
        var response = await user.Client.GetAsync(new Uri("/api/expenses?sortBy=password&pageSize=1000", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("sort_invalid", await response.ErrorCodeAsync("sortBy"));
        Assert.Equal("page_size_invalid", await response.ErrorCodeAsync("pageSize"));
    }

    [Fact]
    public async Task Csv_export_has_a_header_escapes_formulas_and_only_the_users_rows()
    {
        using var user = await api.NewUserAsync();
        using var other = await api.NewUserAsync();
        await CreateAsync(user, 12.5m, note: "=HYPERLINK(\"http://evil\")");
        await CreateAsync(other, 999m, note: "someone else");

        var response = await user.Client.GetAsync(new Uri($"/api/expenses/export?from={Today.AddDays(-1):yyyy-MM-dd}&to={Today:yyyy-MM-dd}", UriKind.Relative));
        var lines = (await response.Content.ReadAsStringAsync()).TrimStart('﻿').Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("attachment", response.Content.Headers.ContentDisposition?.DispositionType, StringComparison.Ordinal);
        Assert.Equal("Date,Category,Amount,Currency,Note", lines[0]);
        Assert.Single(lines.Skip(1));
        Assert.Contains("'=HYPERLINK", lines[1], StringComparison.Ordinal);
        Assert.DoesNotContain("999", string.Join('\n', lines), StringComparison.Ordinal);
    }
}
