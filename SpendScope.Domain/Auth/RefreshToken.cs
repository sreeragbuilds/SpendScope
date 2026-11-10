using SpendScope.Domain.Common;

namespace SpendScope.Domain.Auth;

/// <summary>
/// A refresh token record. Only the SHA-256 hash of the token is stored (NFR-05).
/// Rotation links the old row to its replacement for reuse detection (spec 13.5).
/// </summary>
public sealed class RefreshToken
{
    private RefreshToken()
    {
        // For EF Core.
        TokenHash = string.Empty;
    }

    private RefreshToken(Guid userId, string tokenHash, DateTime createdAtUtc, DateTime expiresAtUtc, string? createdByIp)
    {
        Id = Guid.CreateVersion7();
        UserId = userId;
        TokenHash = tokenHash;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        CreatedByIp = createdByIp;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; }

    public DateTime ExpiresAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? RevokedAtUtc { get; private set; }

    public string? ReplacedByTokenHash { get; private set; }

    public string? CreatedByIp { get; private set; }

    public bool IsRevoked => RevokedAtUtc is not null;

    public static RefreshToken Issue(Guid userId, string tokenHash, DateTime nowUtc, TimeSpan lifetime, string? createdByIp)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new DomainException("refresh_token_invalid", "A refresh token needs a user and a hash.");
        }

        if (lifetime <= TimeSpan.Zero)
        {
            throw new DomainException("refresh_token_invalid", "Refresh token lifetime must be positive.");
        }

        return new RefreshToken(userId, tokenHash, nowUtc, nowUtc + lifetime, createdByIp?[..Math.Min(createdByIp.Length, 45)]);
    }

    public bool IsExpired(DateTime nowUtc) => nowUtc >= ExpiresAtUtc;

    public bool IsActive(DateTime nowUtc) => !IsRevoked && !IsExpired(nowUtc);

    /// <summary>Revokes this token. Idempotent: a revoked token keeps its original revocation time.</summary>
    public void Revoke(DateTime nowUtc, string? replacedByTokenHash = null)
    {
        if (IsRevoked)
        {
            return;
        }

        RevokedAtUtc = nowUtc;
        ReplacedByTokenHash = replacedByTokenHash;
    }
}
