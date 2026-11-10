using SpendScope.Application.Abstractions;
using SpendScope.Contracts.Auth;
using SpendScope.Domain.Auth;

namespace SpendScope.Features.Auth;

/// <summary>
/// Builds an access token plus a new refresh token row. Shared by Login and Refresh (area-level
/// sharing is allowed by ARCH-04). Persisting the row is left to the calling handler, so this
/// class needs no EF Core (ARCH-09).
/// </summary>
public static class AuthTokens
{
    public static (TokenResponse Response, RefreshToken Row) Issue(
        UserAccount user, ITokenService tokens, DateTime nowUtc, string? ipAddress)
    {
        var access = tokens.CreateAccessToken(user);
        var refresh = tokens.CreateRefreshToken();
        var row = RefreshToken.Issue(user.Id, tokens.HashRefreshToken(refresh), nowUtc, tokens.RefreshTokenLifetime, ipAddress);

        return (new TokenResponse(access.Token, refresh, access.ExpiresInSeconds), row);
    }
}
