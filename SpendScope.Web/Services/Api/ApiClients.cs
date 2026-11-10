using System.Globalization;
using System.Net.Http.Json;
using SpendScope.Contracts.Admin;
using SpendScope.Contracts.Auth;
using SpendScope.Contracts.Categories;
using SpendScope.Contracts.Common;
using SpendScope.Contracts.Expenses;
using SpendScope.Contracts.Reports;

namespace SpendScope.Web.Services.Api;

/// <summary>Shared request plumbing: every call returns an <see cref="ApiResult{T}"/>, never throws for HTTP errors.</summary>
public abstract class ApiClientBase(HttpClient http)
{
    protected HttpClient Http => http;

    protected Task<ApiResult<T>> GetAsync<T>(string url, CancellationToken ct) => SendAsync<T>(() => http.GetAsync(url, ct), ct);

    protected Task<ApiResult<T>> PostAsync<T>(string url, object? body, CancellationToken ct) =>
        SendAsync<T>(() => http.PostAsJsonAsync(url, body, ct), ct);

    protected Task<ApiResult<T>> PutAsync<T>(string url, object body, CancellationToken ct) =>
        SendAsync<T>(() => http.PutAsJsonAsync(url, body, ct), ct);

    protected Task<ApiResult<Unit>> DeleteAsync(string url, CancellationToken ct) =>
        SendAsync<Unit>(() => http.DeleteAsync(url, ct), ct);

    private static async Task<ApiResult<T>> SendAsync<T>(Func<Task<HttpResponseMessage>> send, CancellationToken ct)
    {
        try
        {
            using var response = await send();
            return await ApiResult<T>.FromResponseAsync(response, ct);
        }
        catch (HttpRequestException)
        {
            return ApiResult<T>.NetworkFailure();
        }
    }

    protected static string Date(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}

/// <summary>Anonymous account endpoints. Login and refresh go through <see cref="Auth.AuthSession"/>.</summary>
public sealed class AccountApiClient(HttpClient http) : ApiClientBase(http)
{
    public Task<ApiResult<UserDto>> RegisterAsync(RegisterRequest request, CancellationToken ct = default) =>
        PostAsync<UserDto>("api/auth/register", request, ct);

    public Task<ApiResult<Unit>> ForgotPasswordAsync(string email, CancellationToken ct = default) =>
        PostAsync<Unit>("api/auth/forgot-password", new ForgotPasswordRequest(email), ct);

    public Task<ApiResult<Unit>> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default) =>
        PostAsync<Unit>("api/auth/reset-password", request, ct);
}

public sealed class ProfileApiClient(HttpClient http) : ApiClientBase(http)
{
    public Task<ApiResult<UserProfileDto>> GetAsync(CancellationToken ct = default) => GetAsync<UserProfileDto>("api/me", ct);

    public Task<ApiResult<UserProfileDto>> UpdateAsync(UpdateProfileRequest request, CancellationToken ct = default) =>
        PutAsync<UserProfileDto>("api/me", request, ct);

    public Task<ApiResult<Unit>> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct = default) =>
        PostAsync<Unit>("api/auth/change-password", request, ct);
}

public sealed class ExpenseApiClient(HttpClient http) : ApiClientBase(http)
{
    public Task<ApiResult<PagedResponse<ExpenseDto>>> ListAsync(ExpenseQuery query, CancellationToken ct = default) =>
        GetAsync<PagedResponse<ExpenseDto>>("api/expenses" + query.ToQueryString(), ct);

    public Task<ApiResult<ExpenseDto>> GetAsync(Guid id, CancellationToken ct = default) => GetAsync<ExpenseDto>($"api/expenses/{id}", ct);

    public Task<ApiResult<ExpenseDto>> CreateAsync(CreateExpenseRequest request, CancellationToken ct = default) =>
        PostAsync<ExpenseDto>("api/expenses", request, ct);

    public Task<ApiResult<ExpenseDto>> UpdateAsync(Guid id, UpdateExpenseRequest request, CancellationToken ct = default) =>
        PutAsync<ExpenseDto>($"api/expenses/{id}", request, ct);

    public Task<ApiResult<Unit>> DeleteAsync(Guid id, CancellationToken ct = default) => DeleteAsync($"api/expenses/{id}", ct);

    /// <summary>Downloads the CSV through the authenticated client (a plain link would carry no token).</summary>
    public async Task<ApiResult<(string FileName, byte[] Content)>> ExportAsync(ExpenseQuery query, CancellationToken ct = default)
    {
        try
        {
            using var response = await Http.GetAsync("api/expenses/export" + (query with { Page = null, PageSize = null }).ToQueryString(), ct);
            if (!response.IsSuccessStatusCode)
            {
                var failed = await ApiResult<Unit>.FromResponseAsync(response, ct);
                return ApiResult<(string, byte[])>.Fail(failed.Status, failed.Problem);
            }

            var name = response.Content.Headers.ContentDisposition?.FileNameStar
                ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                ?? "spendscope-expenses.csv";
            return ApiResult<(string, byte[])>.Ok((name, await response.Content.ReadAsByteArrayAsync(ct)));
        }
        catch (HttpRequestException)
        {
            return ApiResult<(string, byte[])>.NetworkFailure();
        }
    }
}

public sealed class CategoryApiClient(HttpClient http) : ApiClientBase(http)
{
    public Task<ApiResult<List<CategoryDto>>> ListAsync(CancellationToken ct = default) => GetAsync<List<CategoryDto>>("api/categories", ct);

    public Task<ApiResult<CategoryDto>> CreateAsync(CreateCategoryRequest request, CancellationToken ct = default) =>
        PostAsync<CategoryDto>("api/categories", request, ct);

    public Task<ApiResult<CategoryDto>> UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken ct = default) =>
        PutAsync<CategoryDto>($"api/categories/{id}", request, ct);

    public Task<ApiResult<Unit>> DeleteAsync(Guid id, Guid? replacementId, CancellationToken ct = default) =>
        DeleteAsync($"api/categories/{id}{(replacementId is { } r ? $"?replacementId={r}" : string.Empty)}", ct);
}

public sealed class ReportApiClient(HttpClient http) : ApiClientBase(http)
{
    public Task<ApiResult<CategorySummaryDto>> SummaryAsync(DateOnly from, DateOnly to, CancellationToken ct = default) =>
        GetAsync<CategorySummaryDto>($"api/reports/summary?from={Date(from)}&to={Date(to)}", ct);

    public Task<ApiResult<MonthlyTotalsDto>> MonthlyAsync(int year, CancellationToken ct = default) =>
        GetAsync<MonthlyTotalsDto>($"api/reports/monthly?year={year}", ct);
}

public sealed class AdminApiClient(HttpClient http) : ApiClientBase(http)
{
    public Task<ApiResult<PagedResponse<AdminUserDto>>> ListUsersAsync(int page, int pageSize, string? search, CancellationToken ct = default) =>
        GetAsync<PagedResponse<AdminUserDto>>(
            $"api/admin/users?page={page}&pageSize={pageSize}{(string.IsNullOrWhiteSpace(search) ? string.Empty : "&search=" + Uri.EscapeDataString(search))}",
            ct);

    public Task<ApiResult<Unit>> LockAsync(Guid id, CancellationToken ct = default) => PostAsync<Unit>($"api/admin/users/{id}/lock", null, ct);

    public Task<ApiResult<Unit>> UnlockAsync(Guid id, CancellationToken ct = default) => PostAsync<Unit>($"api/admin/users/{id}/unlock", null, ct);

    public Task<ApiResult<SystemCategoryTotalsDto>> CategoryTotalsAsync(DateOnly from, DateOnly to, CancellationToken ct = default) =>
        GetAsync<SystemCategoryTotalsDto>($"api/admin/reports/category-totals?from={Date(from)}&to={Date(to)}", ct);

    public Task<ApiResult<SystemMonthlyTotalsDto>> MonthlyAsync(int year, CancellationToken ct = default) =>
        GetAsync<SystemMonthlyTotalsDto>($"api/admin/reports/monthly?year={year}", ct);
}
