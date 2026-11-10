namespace SpendScope.Application.Abstractions;

public sealed record AccessToken(string Token, DateTime ExpiresAtUtc, int ExpiresInSeconds);

/// <summary>Issues JWT access tokens and opaque refresh tokens (spec 13.2).</summary>
public interface ITokenService
{
    AccessToken CreateAccessToken(UserAccount user);

    /// <summary>64 random bytes, Base64Url encoded. Return it to the client; persist only its hash.</summary>
    string CreateRefreshToken();

    /// <summary>SHA-256 of the token, Base64 encoded.</summary>
    string HashRefreshToken(string refreshToken);

    TimeSpan RefreshTokenLifetime { get; }
}
