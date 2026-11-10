using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpendScope.Domain.Categories;
using SpendScope.Infrastructure.Identity;

namespace SpendScope.Infrastructure.Persistence.Configurations;

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories", t =>
        {
            t.HasCheckConstraint("CK_Categories_ColorHex", "\"ColorHex\" ~ '^#[0-9A-Fa-f]{6}$'");
            t.HasCheckConstraint("CK_Categories_IsSystem_Matches_UserId", "\"IsSystem\" = (\"UserId\" IS NULL)");
        });

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Name).HasMaxLength(CategoryRules.NameMaxLength).IsRequired();
        builder.Property(c => c.NormalizedName).HasMaxLength(CategoryRules.NameMaxLength).IsRequired();
        builder.Property(c => c.ColorHex).HasColumnType("char(7)").IsRequired();

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Names are unique per user, and system names are unique globally (spec 9.2).
        builder.HasIndex(c => new { c.UserId, c.NormalizedName })
            .IsUnique()
            .HasFilter("\"UserId\" IS NOT NULL")
            .HasDatabaseName("IX_Categories_UserId_NormalizedName");
        builder.HasIndex(c => c.NormalizedName)
            .IsUnique()
            .HasFilter("\"UserId\" IS NULL")
            .HasDatabaseName("IX_Categories_System_NormalizedName");

        builder.HasData(SystemCategories.All);
    }
}
