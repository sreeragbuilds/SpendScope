using SpendScope.Domain.Common;
using SpendScope.Domain.Expenses;
using SpendScope.Domain.ValueObjects;

namespace SpendScope.UnitTests.Domain;

/// <summary>Expense invariants (spec 8.1; FR-EXP-02 and 03).</summary>
[TestFixture]
public sealed class ExpenseTests
{
    private static readonly DateTime Now = FixedClock.Default;
    private static readonly DateOnly Today = DateOnly.FromDateTime(Now);
    private static readonly Guid User = Guid.NewGuid();
    private static readonly Guid Category = Guid.NewGuid();

    private static Expense Create(decimal amount = 42.50m, DateOnly? on = null, string? note = null) =>
        Expense.Create(User, new Money(amount, "USD"), on ?? Today, Category, note, Now);

    [Test]
    public void Create_sets_every_field_and_a_time_ordered_id()
    {
        var expense = Create(note: "  Lunch with team  ");

        Assert.Multiple(() =>
        {
            Assert.That(expense.Id, Is.Not.EqualTo(Guid.Empty));
            Assert.That(expense.Id.Version, Is.EqualTo(7), "Guid.CreateVersion7 (spec 8.1)");
            Assert.That(expense.UserId, Is.EqualTo(User));
            Assert.That(expense.Amount, Is.EqualTo(42.50m));
            Assert.That(expense.Currency, Is.EqualTo("USD"));
            Assert.That(expense.OccurredOn, Is.EqualTo(Today));
            Assert.That(expense.Note, Is.EqualTo("Lunch with team"), "note is trimmed");
            Assert.That(expense.CreatedAtUtc, Is.EqualTo(Now));
            Assert.That(expense.UpdatedAtUtc, Is.Null);
        });
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(1_000_000.01)]
    [TestCase(10.123)]
    public void Create_rejects_invalid_amounts(decimal amount)
    {
        var ex = Assert.Throws<DomainException>(() => Create(amount));
        Assert.That(ex!.Code, Is.EqualTo("amount_range"));
    }

    [TestCase(0.01)]
    [TestCase(1_000_000.00)]
    public void Create_accepts_boundary_amounts(decimal amount) =>
        Assert.That(Create(amount).Amount, Is.EqualTo(amount));

    [Test]
    public void Create_accepts_exactly_one_year_ahead_and_rejects_one_day_more()
    {
        Assert.That(() => Create(on: Today.AddDays(365)), Throws.Nothing);

        var ex = Assert.Throws<DomainException>(() => Create(on: Today.AddDays(366)));
        Assert.That(ex!.Code, Is.EqualTo("date_future"));
    }

    [Test]
    public void Create_rejects_a_note_over_500_characters() =>
        Assert.That(
            () => Create(note: new string('x', 501)),
            Throws.TypeOf<DomainException>().With.Property(nameof(DomainException.Code)).EqualTo("note_length"));

    [Test]
    public void Blank_note_becomes_null() => Assert.That(Create(note: "   ").Note, Is.Null);

    [Test]
    public void Create_requires_a_user_and_a_category()
    {
        Assert.Throws<DomainException>(() => Expense.Create(Guid.Empty, new Money(1, "USD"), Today, Category, null, Now));
        Assert.Throws<DomainException>(() => Expense.Create(User, new Money(1, "USD"), Today, Guid.Empty, null, Now));
    }

    [Test]
    public void Update_changes_fields_sets_UpdatedAtUtc_and_keeps_currency()
    {
        var expense = Create();
        var later = Now.AddHours(2);
        var newCategory = Guid.NewGuid();

        expense.Update(10m, Today.AddDays(-1), newCategory, "edited", later);

        Assert.Multiple(() =>
        {
            Assert.That(expense.Amount, Is.EqualTo(10m));
            Assert.That(expense.OccurredOn, Is.EqualTo(Today.AddDays(-1)));
            Assert.That(expense.CategoryId, Is.EqualTo(newCategory));
            Assert.That(expense.Note, Is.EqualTo("edited"));
            Assert.That(expense.UpdatedAtUtc, Is.EqualTo(later));
            Assert.That(expense.Currency, Is.EqualTo("USD"), "currency is not editable (spec 15.9)");
        });
    }

    [Test]
    public void Update_enforces_the_same_invariants_as_create()
    {
        var expense = Create();
        Assert.Throws<DomainException>(() => expense.Update(0, Today, Category, null, Now));
        Assert.Throws<DomainException>(() => expense.ChangeAmount(-5, Now));
        Assert.That(expense.Amount, Is.EqualTo(42.50m), "a rejected update leaves the expense unchanged");
    }
}
