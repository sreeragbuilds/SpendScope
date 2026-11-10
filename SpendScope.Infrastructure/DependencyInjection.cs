using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SpendScope.Application.Abstractions;
using SpendScope.Domain.Common;
using SpendScope.Infrastructure.Identity;
using SpendScope.Infrastructure.Persistence;
using SpendScope.Infrastructure.Services;

namespace SpendScope.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var connectionString = DatabaseConnection.Resolve(configuration);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'ConnectionStrings:Default' is not configured. " +
                "In development set it with dotnet user-secrets (see README); elsewhere use ConnectionStrings__Default or DATABASE_URL.");
        }

        services.AddDbContext<AppDbContext>(options => options
            .UseNpgsql(connectionString)
            // Expense -> Category is required and Category has a filter; the filters are designed so
            // a visible expense always has a visible category, so this warning does not apply.
            .ConfigureWarnings(w => w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning)));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                // Spec 13.3 password policy.
                options.Password.RequiredLength = 10;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequiredUniqueChars = 4;

                // Spec 13.4 lockout.
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(configuration.GetValue("Identity:LockoutMinutes", 15));
                options.Lockout.MaxFailedAccessAttempts = configuration.GetValue("Identity:MaxFailedAttempts", 5);
                options.Lockout.AllowedForNewUsers = true;

                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        // Keys in PostgreSQL, not the container filesystem: restarts (e.g. Render free-tier spin-down)
        // would otherwise invalidate every outstanding reset link.
        services.AddDataProtection()
            .SetApplicationName("SpendScope")
            .PersistKeysToDbContext<AppDbContext>();

        // Reset links are single use (the security stamp changes) and expire after an hour.
        services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromHours(1));

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddScoped<IUserAccounts, UserAccounts>();

        var emailProvider = configuration["Email:Provider"] ?? "Console";
        if (emailProvider != "Console")
        {
            throw new InvalidOperationException($"Email provider '{emailProvider}' is not implemented yet. Only 'Console' is available.");
        }

        // The console sender logs reset tokens, so only Development and Testing get it. Elsewhere,
        // until SMTP is implemented, emails are dropped with a warning that omits the body.
        if (environment.IsDevelopment() || environment.IsEnvironment("Testing"))
        {
            services.AddSingleton<IEmailSender, ConsoleEmailSender>();
        }
        else
        {
            services.AddSingleton<IEmailSender, DisabledEmailSender>();
        }

        return services;
    }
}
