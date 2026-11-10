using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SpendScope.Contracts.Auth;
using SpendScope.Web.Services.Api;
using SpendScope.Web.Services.Browser;

namespace SpendScope.Web.Services.Auth;

/// <summary>
/// Owns the sign-in lifecycle: login, refresh (single-flight), logout, and restoring a session
/// from sessionStorage after a page reload. Uses the "auth" HttpClient, which has no auth
/// handlers, so refresh can never recurse.
/// </summary>
public sealed class AuthSession(IHttpClientFactory httpFactory, TokenStore tokens, BrowserStorage storage) : IDisposable
{
    public const string AuthClient = "auth";
    private const string RefreshKey = "spendscope.refresh";

    private readonly SemaphoreSlim _gate = new(1, 1);
    private Task<bool>? _refreshInFlight;
    private Task? _initialization;

    /// <summary>Raised after login, logout, refresh, and forced sign-out.</summary>
    public event Action? Changed;

    public string? AccessToken => tokens.AccessToken;

    /// <summary>Runs once: tries to restore the session from a stored refresh token.</summary>
    public Task EnsureInitializedAsync() => _initialization ??= RestoreAsync();

    public async Task<ApiResult<TokenResponse>> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        var result = await PostAsync<TokenResponse>("api/auth/login", new LoginRequest(email, password), ct);
        if (result.IsSuccess)
        {
            await AcceptAsync(result.Value!);
        }

        return result;
    }

    /// <summary>
    /// Exchanges the refresh token for new tokens. Concurrent callers share one request
    /// (spec 19.4 "single-flight"). Returns false and signs out locally on failure.
    /// </summary>
    public async Task<bool> RefreshAsync()
    {
        Task<bool> task;
        await _gate.WaitAsync();
        try
        {
            task = _refreshInFlight ??= RefreshCoreAsync();
        }
        finally
        {
            _gate.Release();
        }

        try
        {
            return await task;
        }
        finally
        {
            await _gate.WaitAsync();
            if (ReferenceEquals(_refreshInFlight, task))
            {
                _refreshInFlight = null;
            }

            _gate.Release();
        }
    }

    public async Task LogoutAsync()
    {
        var refresh = tokens.RefreshToken;
        var access = tokens.AccessToken;
        if (refresh is not null && access is not null)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/logout")
                {
                    Content = JsonContent.Create(new LogoutRequest(refresh)),
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
                using var _ = await httpFactory.CreateClient(AuthClient).SendAsync(request);
            }
            catch (HttpRequestException)
            {
                // Offline: the server-side token expires on its own; local sign-out still happens.
            }
        }

        await SignOutLocallyAsync();
    }

    public async Task SignOutLocallyAsync()
    {
        tokens.Clear();
        await storage.SessionRemoveAsync(RefreshKey);
        Changed?.Invoke();
    }

    private async Task RestoreAsync()
    {
        var stored = await storage.SessionGetAsync(RefreshKey);
        if (string.IsNullOrEmpty(stored))
        {
            return;
        }

        tokens.SetRefreshTokenOnly(stored);

        // Through the single-flight gate: if an API call starts refreshing at the same moment, both
        // share one request. Two refreshes with one token would trip the server's reuse detection.
        await RefreshAsync();
    }

    private async Task<bool> RefreshCoreAsync()
    {
        var refresh = tokens.RefreshToken;
        if (refresh is null)
        {
            return false;
        }

        var result = await PostAsync<TokenResponse>("api/auth/refresh", new RefreshRequest(refresh), CancellationToken.None);
        if (result.IsSuccess)
        {
            await AcceptAsync(result.Value!);
            return true;
        }

        // Network failure: keep the refresh token so a later attempt can succeed.
        if (result.Status != 0)
        {
            await SignOutLocallyAsync();
        }

        return false;
    }

    private async Task AcceptAsync(TokenResponse response)
    {
        tokens.Set(response.AccessToken, response.ExpiresIn, response.RefreshToken);
        await storage.SessionSetAsync(RefreshKey, response.RefreshToken);
        Changed?.Invoke();
    }

    private async Task<ApiResult<T>> PostAsync<T>(string url, object body, CancellationToken ct)
    {
        try
        {
            using var response = await httpFactory.CreateClient(AuthClient).PostAsJsonAsync(url, body, ct);
            return await ApiResult<T>.FromResponseAsync(response, ct);
        }
        catch (HttpRequestException)
        {
            return ApiResult<T>.NetworkFailure();
        }
    }

    public void Dispose() => _gate.Dispose();

    internal static bool IsUnauthorized(HttpResponseMessage response) => response.StatusCode == HttpStatusCode.Unauthorized;
}
