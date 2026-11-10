using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SpendScope.Application.Abstractions;
using SpendScope.Domain.Auth;
using SpendScope.Domain.Categories;
using SpendScope.Domain.Expenses;
using SpendScope.Infrastructure.Identity;

namespace SpendScope.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUser currentUser)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options), IAppDbContext
{
    public DbSet<Expense> Expenses => Set<Expense>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    /// <summary>
    /// Read by the global query filters. A property on the context (not a captured variable) so
    /// EF Core parameterizes it per query instead of baking one user's ID into the cached model.
    /// Anonymous requests get <see cref="Guid.Empty"/>, which matches no rows.
    /// </summary>
    private Guid CurrentUserId => currentUser.UserId;

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        Database.BeginTransactionAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Defense in depth (spec 13.9): every query sees only the caller's rows unless a reviewed
        // admin path calls IgnoreQueryFilters().
        builder.Entity<Expense>().HasQueryFilter(e => e.UserId == CurrentUserId);
        builder.Entity<Category>().HasQueryFilter(c => c.UserId == null || c.UserId == CurrentUserId);
    }
}
