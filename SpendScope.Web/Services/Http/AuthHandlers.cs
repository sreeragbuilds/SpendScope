using System.Net.Http.Headers;
using SpendScope.Web.Services.Auth;

namespace SpendScope.Web.Services.Http;

/// <summary>
/// Spec 19.4. Adds the bearer token, refreshing first if it expires within 30 seconds.
/// Registered first, so it is the outer handler.
/// </summary>
public sealed class AuthorizationHeaderHandler(TokenStore tokens, AuthSession session) : DelegatingHandler
{
    private static readonly TimeSpan Margin = TimeSpan.FromSeconds(30);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // A call made during startup waits for the session restore instead of racing it.
        await session.EnsureInitializedAsync();

        if (!tokens.IsFresh(Margin) && tokens.RefreshToken is not null)
        {
            await session.RefreshAsync();
        }

        if (tokens.AccessToken is { } token)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}

/// <summary>
/// Spec 19.4. On 401, refreshes once and retries the request once. If that fails too, the
/// session is signed out and the router sends the user to /login.
/// </summary>
public sealed class RefreshOnUnauthorizedHandler(TokenStore tokens, AuthSession session) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Buffer the body so the request can be replayed after a refresh.
        byte[]? body = null;
        if (request.Content is not null)
        {
            body = await request.Content.ReadAsByteArrayAsync(cancellationToken);
        }

        var response = await base.SendAsync(request, cancellationToken);
        if (!AuthSession.IsUnauthorized(response) || tokens.RefreshToken is null)
        {
            return response;
        }

        if (!await session.RefreshAsync())
        {
            return response;
        }

        response.Dispose();
        using var retry = Clone(request, body);
        retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var retried = await base.SendAsync(retry, cancellationToken);
        if (AuthSession.IsUnauthorized(retried))
        {
            await session.SignOutLocallyAsync();
        }

        return retried;
    }

    private static HttpRequestMessage Clone(HttpRequestMessage original, byte[]? body)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri) { Version = original.Version };
        foreach (var header in original.Headers.Where(h => h.Key != "Authorization"))
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (body is not null)
        {
            clone.Content = new ByteArrayContent(body);
            foreach (var header in original.Content!.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return clone;
    }
}
