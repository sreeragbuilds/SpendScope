using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpendScope.Application.Common;
using SpendScope.Infrastructure.Identity;

namespace SpendScope.Infrastructure.Persistence.Configurations;

internal sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(u => u.DisplayName).HasMaxLength(ApplicationUser.DisplayNameMaxLength).IsRequired();
        builder.Property(u => u.DefaultCurrency).HasColumnType("char(3)").HasDefaultValue("USD").IsRequired();
    }
}

/// <summary>
/// Roles are seeded by migration (not at startup) so every environment, including production,
/// gets them from the migration bundle. IDs and stamps are fixed so the model is stable.
/// </summary>
internal sealed class RoleConfiguration : IEntityTypeConfiguration<IdentityRole<Guid>>
{
    public static readonly Guid UserRoleId = new("0196a3f0-0000-7000-8000-000000000001");
    public static readonly Guid AdminRoleId = new("0196a3f0-0000-7000-8000-000000000002");

    public void Configure(EntityTypeBuilder<IdentityRole<Guid>> builder) =>
        builder.HasData(
            new IdentityRole<Guid>
            {
                Id = UserRoleId,
                Name = Roles.User,
                NormalizedName = Roles.User.ToUpperInvariant(),
                ConcurrencyStamp = "4f0d7d3c-user",
            },
            new IdentityRole<Guid>
            {
                Id = AdminRoleId,
                Name = Roles.Admin,
                NormalizedName = Roles.Admin.ToUpperInvariant(),
                ConcurrencyStamp = "4f0d7d3c-admin",
            });
}
