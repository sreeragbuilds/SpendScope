using SpendScope.Application.Abstractions;
using SpendScope.Infrastructure.Identity;

namespace SpendScope.Api.Auth;

/// <summary>Resolves the caller from the validated access token on the current request.</summary>
internal sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid UserId =>
        Guid.TryParse(accessor.HttpContext?.User.FindFirst(TokenClaims.Subject)?.Value, out var id) ? id : Guid.Empty;

    public bool IsAuthenticated => accessor.HttpContext?.User.Identity?.IsAuthenticated == true;

    public bool IsInRole(string role) => accessor.HttpContext?.User.IsInRole(role) == true;

    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
}
