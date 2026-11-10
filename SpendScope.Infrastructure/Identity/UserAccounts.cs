using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SpendScope.Application.Abstractions;
using SpendScope.Application.Common;
using SpendScope.Domain.Common;
using SpendScope.Infrastructure.Persistence;

namespace SpendScope.Infrastructure.Identity;

internal sealed class UserAccounts(
    UserManager<ApplicationUser> users,
    IPasswordHasher<ApplicationUser> hasher,
    AppDbContext db,
    IClock clock) : IUserAccounts
{
    // A real hash, computed once, so unknown-email logins spend the same time hashing as real ones.
    private static readonly Lazy<string> DummyHash = new(() =>
        new PasswordHasher<ApplicationUser>().HashPassword(new ApplicationUser(), Guid.NewGuid().ToString()));

    public async Task<Result<UserAccount>> RegisterAsync(string email, string password, string displayName, CancellationToken cancellationToken)
    {
        if (await users.FindByEmailAsync(email) is not null)
        {
            return Error.Conflict("email_taken", "An account with this email already exists.", "email");
        }

        var user = new ApplicationUser
        {
            Email = email,
            UserName = email,
            DisplayName = displayName,
            DefaultCurrency = "USD",
            CreatedAtUtc = clock.UtcNow,
        };

        var created = await users.CreateAsync(user, password);
        if (!created.Succeeded)
        {
            return created.Errors.Any(e => e.Code is "DuplicateEmail" or "DuplicateUserName")
                ? Error.Conflict("email_taken", "An account with this email already exists.", "email")
                : Error.Validation("password_policy", Describe(created), "password");
        }

        var roleAdded = await users.AddToRoleAsync(user, Roles.User);
        if (!roleAdded.Succeeded)
        {
            return Error.Failure("role_assignment_failed", Describe(roleAdded));
        }

        return await ToAccountAsync(user);
    }

    public async Task<PasswordCheckResult> CheckPasswordAsync(string email, string password, CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            hasher.VerifyHashedPassword(new ApplicationUser(), DummyHash.Value, password);
            return new PasswordCheckResult(PasswordCheckStatus.InvalidCredentials, null, null);
        }

        if (await users.IsLockedOutAsync(user))
        {
            return new PasswordCheckResult(PasswordCheckStatus.LockedOut, null, RetryAfter(user));
        }

        if (!await users.CheckPasswordAsync(user, password))
        {
            await users.AccessFailedAsync(user);
            return await users.IsLockedOutAsync(user)
                ? new PasswordCheckResult(PasswordCheckStatus.LockedOut, null, RetryAfter(user))
                : new PasswordCheckResult(PasswordCheckStatus.InvalidCredentials, null, null);
        }

        await users.ResetAccessFailedCountAsync(user);
        return new PasswordCheckResult(PasswordCheckStatus.Succeeded, await ToAccountAsync(user), null);
    }

    public async Task<UserAccount?> FindByIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        return user is null ? null : await ToAccountAsync(user);
    }

    public Task<string?> GetDefaultCurrencyAsync(Guid userId, CancellationToken cancellationToken) =>
        db.Users.Where(u => u.Id == userId).Select(u => u.DefaultCurrency).SingleOrDefaultAsync(cancellationToken);

    public async Task<Result> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return Error.Unauthorized("user_not_found", "The signed-in user no longer exists.");
        }

        var changed = await users.ChangePasswordAsync(user, currentPassword, newPassword);
        if (changed.Succeeded)
        {
            return Result.Success;
        }

        return changed.Errors.Any(e => e.Code == "PasswordMismatch")
            ? Error.Validation("password_incorrect", "The current password is incorrect.", "currentPassword")
            : Error.Validation("password_policy", Describe(changed), "newPassword");
    }

    public async Task<(UserAccount Account, string Token)?> CreatePasswordResetTokenAsync(string email, CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(email);
        if (user is null || await users.IsLockedOutAsync(user))
        {
            return null;
        }

        var token = await users.GeneratePasswordResetTokenAsync(user);
        return (await ToAccountAsync(user), token);
    }

    public async Task<Result<Guid>> ResetPasswordAsync(string email, string token, string newPassword, CancellationToken cancellationToken)
    {
        var invalid = Error.Validation("invalid_token", "The reset link is invalid or has expired.", "token");

        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            return invalid;
        }

        var reset = await users.ResetPasswordAsync(user, token, newPassword);
        if (reset.Succeeded)
        {
            return user.Id;
        }

        return reset.Errors.Any(e => e.Code == "InvalidToken")
            ? invalid
            : Error.Validation("password_policy", Describe(reset), "newPassword");
    }

    public async Task<Result<UserAccount>> UpdateProfileAsync(Guid userId, string displayName, string defaultCurrency, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return Error.Unauthorized("user_not_found", "The signed-in user no longer exists.");
        }

        user.DisplayName = displayName;
        user.DefaultCurrency = defaultCurrency;
        var updated = await users.UpdateAsync(user);

        return updated.Succeeded
            ? await ToAccountAsync(user)
            : Error.Failure("profile_update_failed", Describe(updated));
    }

    public async Task<PagedResult<UserAccount>> ListAsync(int page, int pageSize, string? emailPrefix, CancellationToken cancellationToken)
    {
        var query = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(emailPrefix))
        {
            var normalized = emailPrefix.Trim().ToUpperInvariant();
            query = query.Where(u => u.NormalizedEmail!.StartsWith(normalized));
        }

        var total = await query.CountAsync(cancellationToken);
        var pageUsers = await query
            .OrderBy(u => u.NormalizedEmail)
            .ThenBy(u => u.Id)
            .Skip(Pagination.Skip(page, pageSize))
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var ids = pageUsers.Select(u => u.Id).ToList();
        var roles = await db.UserRoles
            .Where(ur => ids.Contains(ur.UserId))
            .Join(db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.Name })
            .ToListAsync(cancellationToken);

        var now = clock.UtcNow;
        var items = pageUsers
            .Select(u => ToAccount(u, roles.Where(r => r.UserId == u.Id).Select(r => r.Name!).Order().ToList(), now))
            .ToList();

        return new PagedResult<UserAccount>(items, page, pageSize, total);
    }

    public async Task<Result> SetLockedAsync(Guid userId, bool locked, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return Error.NotFound("user_not_found", "User not found.");
        }

        await users.SetLockoutEnabledAsync(user, true);
        var result = await users.SetLockoutEndDateAsync(user, locked ? DateTimeOffset.MaxValue : null);
        if (!locked)
        {
            await users.ResetAccessFailedCountAsync(user);
        }

        return result.Succeeded ? Result.Success : Error.Failure("lockout_update_failed", Describe(result));
    }

    private async Task<UserAccount> ToAccountAsync(ApplicationUser user)
    {
        var roles = await users.GetRolesAsync(user);
        return ToAccount(user, roles.Order().ToList(), clock.UtcNow);
    }

    private static UserAccount ToAccount(ApplicationUser user, IReadOnlyList<string> roles, DateTime nowUtc) =>
        new(
            user.Id,
            user.Email ?? string.Empty,
            user.DisplayName,
            user.DefaultCurrency,
            roles,
            user.LockoutEnd is { } end && end.UtcDateTime > nowUtc,
            user.CreatedAtUtc);

    private TimeSpan? RetryAfter(ApplicationUser user) =>
        user.LockoutEnd is { } end && end != DateTimeOffset.MaxValue
            ? end.UtcDateTime - clock.UtcNow
            : null;

    private static string Describe(IdentityResult result) =>
        string.Join(" ", result.Errors.Select(e => e.Description));
}
