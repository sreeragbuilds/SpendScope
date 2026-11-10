using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpendScope.Domain.Expenses;
using SpendScope.Infrastructure.Identity;

namespace SpendScope.Infrastructure.Persistence.Configurations;

internal sealed class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.ToTable("Expenses", t => t.HasCheckConstraint("CK_Expenses_Amount_Positive", "\"Amount\" > 0"));

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Amount).HasColumnType("numeric(18,2)");
        builder.Property(e => e.Currency).HasColumnType("char(3)").IsRequired();
        builder.Property(e => e.Note).HasMaxLength(ExpenseRules.NoteMaxLength);
        builder.Ignore(e => e.Money);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Category)
            .WithMany()
            .HasForeignKey(e => e.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // Spec 9.2: main list / date range, and category reports.
        builder.HasIndex(e => new { e.UserId, e.OccurredOn })
            .IsDescending(false, true)
            .HasDatabaseName("IX_Expenses_UserId_OccurredOn");
        builder.HasIndex(e => new { e.UserId, e.CategoryId, e.OccurredOn })
            .HasDatabaseName("IX_Expenses_UserId_CategoryId_OccurredOn");
    }
}
