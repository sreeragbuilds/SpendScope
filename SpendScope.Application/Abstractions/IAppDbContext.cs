using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SpendScope.Domain.Auth;
using SpendScope.Domain.Categories;
using SpendScope.Domain.Expenses;

namespace SpendScope.Application.Abstractions;

/// <summary>
/// The data access surface for slices. Expenses and Categories carry global query filters that
/// scope them to the current user (spec A.4); use <c>IgnoreQueryFilters()</c> only in reviewed
/// admin aggregate code.
/// </summary>
public interface IAppDbContext
{
    DbSet<Expense> Expenses { get; }

    DbSet<Category> Categories { get; }

    DbSet<RefreshToken> RefreshTokens { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
