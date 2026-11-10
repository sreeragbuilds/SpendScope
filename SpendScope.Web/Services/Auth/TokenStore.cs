namespace SpendScope.Web.Services.Auth;

/// <summary>
/// Spec 13.7: the access token lives only in memory; the refresh token is mirrored to
/// sessionStorage (by <see cref="AuthSession"/>) so a page reload can restore the session.
/// </summary>
public sealed class TokenStore
{
    public string? AccessToken { get; private set; }

    public DateTimeOffset AccessTokenExpiresAt { get; private set; }

    public string? RefreshToken { get; private set; }

    public bool HasAccessToken => AccessToken is not null;

    /// <summary>True if the access token exists and is good for more than <paramref name="margin"/>.</summary>
    public bool IsFresh(TimeSpan margin) => AccessToken is not null && AccessTokenExpiresAt - DateTimeOffset.UtcNow > margin;

    public void Set(string accessToken, int expiresInSeconds, string refreshToken)
    {
        AccessToken = accessToken;
        AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresInSeconds);
        RefreshToken = refreshToken;
    }

    public void SetRefreshTokenOnly(string refreshToken) => RefreshToken = refreshToken;

    public void Clear()
    {
        AccessToken = null;
        RefreshToken = null;
        AccessTokenExpiresAt = default;
    }
}
