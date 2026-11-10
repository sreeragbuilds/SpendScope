using Microsoft.AspNetCore.Components.Authorization;

namespace SpendScope.Web.Services.Auth;

/// <summary>Spec 19.4. Builds the user from the access token and re-publishes on every change.</summary>
public sealed class JwtAuthStateProvider : AuthenticationStateProvider, IDisposable
{
    private readonly AuthSession _session;

    public JwtAuthStateProvider(AuthSession session)
    {
        _session = session;
        _session.Changed += OnChanged;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        await _session.EnsureInitializedAsync();
        return new AuthenticationState(JwtClaims.ToPrincipal(_session.AccessToken));
    }

    public void Dispose() => _session.Changed -= OnChanged;

    private void OnChanged() =>
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(JwtClaims.ToPrincipal(_session.AccessToken))));
}
