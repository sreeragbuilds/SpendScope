using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SpendScope.Application.Abstractions;

namespace SpendScope.Infrastructure.Persistence;

/// <summary>
/// Lets <c>dotnet ef migrations add</c> build the model without starting the Api (no secrets,
/// no database connection needed). <c>database update</c> takes a real connection string via
/// <c>--connection</c> or the ConnectionStrings__Default environment variable.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Port=5432;Database=spendscope;Username=spendscope;Password=localdev";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new AppDbContext(options, new NoUser());
    }

    private sealed class NoUser : ICurrentUser
    {
        public Guid UserId => Guid.Empty;

        public bool IsAuthenticated => false;

        public string? IpAddress => null;

        public bool IsInRole(string role) => false;
    }
}
