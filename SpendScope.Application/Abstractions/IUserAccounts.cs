using SpendScope.Application.Common;

namespace SpendScope.Application.Abstractions;

public sealed record UserAccount(
    Guid Id,
    string Email,
    string DisplayName,
    string DefaultCurrency,
    IReadOnlyList<string> Roles,
    bool IsLocked,
    DateTime CreatedAtUtc);

public enum PasswordCheckStatus
{
    Succeeded,
    InvalidCredentials,
    LockedOut,
}

public sealed record PasswordCheckResult(PasswordCheckStatus Status, UserAccount? Account, TimeSpan? RetryAfter);

/// <summary>
/// User account operations backed by ASP.NET Core Identity in Infrastructure. Keeps Identity
/// types out of Domain and Features (ARCH-01, ARCH-03).
/// </summary>
public interface IUserAccounts
{
    /// <summary>Creates the user, assigns the User role. Conflict when the email is taken.</summary>
    Task<Result<UserAccount>> RegisterAsync(string email, string password, string displayName, CancellationToken cancellationToken);

    /// <summary>
    /// Checks a password with lockout on failure. Unknown emails take the same time and return
    /// the same status as a wrong password.
    /// </summary>
    Task<PasswordCheckResult> CheckPasswordAsync(string email, string password, CancellationToken cancellationToken);

    Task<UserAccount?> FindByIdAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Single-column lookup used when creating expenses and building reports.</summary>
    Task<string?> GetDefaultCurrencyAsync(Guid userId, CancellationToken cancellationToken);

    Task<Result> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken);

    /// <summary>Null when the email is unknown or the account is locked.</summary>
    Task<(UserAccount Account, string Token)?> CreatePasswordResetTokenAsync(string email, CancellationToken cancellationToken);

    /// <summary>Returns the user ID on success so callers can revoke sessions.</summary>
    Task<Result<Guid>> ResetPasswordAsync(string email, string token, string newPassword, CancellationToken cancellationToken);

    Task<Result<UserAccount>> UpdateProfileAsync(Guid userId, string displayName, string defaultCurrency, CancellationToken cancellationToken);

    Task<PagedResult<UserAccount>> ListAsync(int page, int pageSize, string? emailPrefix, CancellationToken cancellationToken);

    Task<Result> SetLockedAsync(Guid userId, bool locked, CancellationToken cancellationToken);
}
