using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using SpendScope.Application.Abstractions;
using SpendScope.Application.Common;
using SpendScope.Domain.Auth;
using SpendScope.Domain.Categories;
using SpendScope.Domain.Common;
using SpendScope.Domain.Expenses;

namespace SpendScope.UnitTests;

internal sealed class FixedClock(DateTime utcNow) : IClock
{
    public static readonly DateTime Default = new(2026, 3, 15, 10, 30, 0, DateTimeKind.Utc);

    public FixedClock()
        : this(Default)
    {
    }

    public DateTime UtcNow { get; set; } = utcNow;
}

internal sealed class FakeCurrentUser(Guid userId) : ICurrentUser
{
    public Guid UserId { get; set; } = userId;

    public bool IsAuthenticated => UserId != Guid.Empty;

    public string? IpAddress => "127.0.0.1";

    public bool IsInRole(string role) => role == Roles.User;
}

/// <summary>Only the members the handler tests need; everything else fails loudly.</summary>
internal sealed class FakeUserAccounts(string currency = "EUR") : IUserAccounts
{
    public Task<string?> GetDefaultCurrencyAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult<string?>(currency);

    public Task<Result<UserAccount>> RegisterAsync(string email, string password, string displayName, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<PasswordCheckResult> CheckPasswordAsync(string email, string password, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<UserAccount?> FindByIdAsync(Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<Result> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<(UserAccount Account, string Token)?> CreatePasswordResetTokenAsync(string email, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<Result<Guid>> ResetPasswordAsync(string email, string token, string newPassword, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<Result<UserAccount>> UpdateProfileAsync(Guid userId, string displayName, string defaultCurrency, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<PagedResult<UserAccount>> ListAsync(int page, int pageSize, string? emailPrefix, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<Result> SetLockedAsync(Guid userId, bool locked, CancellationToken cancellationToken) => throw new NotSupportedException();
}

/// <summary>
/// In-memory stand-in for AppDbContext with the same per-user query filters, so handler tests
/// exercise the isolation rules without PostgreSQL.
/// </summary>
internal sealed class TestDbContext(DbContextOptions<TestDbContext> options, ICurrentUser currentUser) : DbContext(options), IAppDbContext
{
    public DbSet<Expense> Expenses => Set<Expense>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    private Guid CurrentUserId => currentUser.UserId;

    public static TestDbContext Create(ICurrentUser user) =>
        new(
            new DbContextOptionsBuilder<TestDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options,
            user);

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        Database.BeginTransactionAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Expense>().Ignore(e => e.Money);
        modelBuilder.Entity<Expense>().HasOne(e => e.Category).WithMany().HasForeignKey(e => e.CategoryId);
        modelBuilder.Entity<RefreshToken>().Ignore(t => t.IsRevoked);
        modelBuilder.Entity<Expense>().HasQueryFilter(e => e.UserId == CurrentUserId);
        modelBuilder.Entity<Category>().HasQueryFilter(c => c.UserId == null || c.UserId == CurrentUserId);
    }
}
