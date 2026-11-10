using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SpendScope.Application.Common;
using SpendScope.Domain.Common;
using SpendScope.Infrastructure.Identity;

namespace SpendScope.Infrastructure.Persistence.Seeding;

public static class DatabaseInitializer
{
    /// <summary>
    /// Applies pending migrations when "Database:MigrateOnStartup" is true (Development and tests
    /// only; production runs the migration bundle, spec 25.4). Then creates the development
    /// admin from Seed:Admin:Email / Seed:Admin:Password if both are set.
    /// </summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, IConfiguration configuration, bool seedDevelopmentAdmin)
    {
        await using var scope = services.CreateAsyncScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseInitializer));

        if (configuration.GetValue<bool>("Database:MigrateOnStartup"))
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
            logger.LogInformation("Database migrations applied");
        }

        if (seedDevelopmentAdmin)
        {
            await SeedAdminAsync(scope.ServiceProvider, configuration, logger);
        }
    }

    private static async Task SeedAdminAsync(IServiceProvider services, IConfiguration configuration, ILogger logger)
    {
        var email = configuration["Seed:Admin:Email"];
        var password = configuration["Seed:Admin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = await users.FindByEmailAsync(email);
        if (admin is null)
        {
            admin = new ApplicationUser
            {
                Email = email,
                UserName = email,
                DisplayName = "Administrator",
                EmailConfirmed = true,
                CreatedAtUtc = services.GetRequiredService<IClock>().UtcNow,
            };

            var created = await users.CreateAsync(admin, password);
            if (!created.Succeeded)
            {
                logger.LogWarning("Development admin not created: {Errors}", string.Join(" ", created.Errors.Select(e => e.Description)));
                return;
            }

            logger.LogInformation("Development admin {Email} created", email);
        }

        foreach (var role in new[] { Roles.User, Roles.Admin })
        {
            if (!await users.IsInRoleAsync(admin, role))
            {
                await users.AddToRoleAsync(admin, role);
            }
        }
    }
}
