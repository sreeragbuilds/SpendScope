using Microsoft.EntityFrameworkCore;
using SpendScope.Application.Common;
using SpendScope.Contracts.Expenses;
using SpendScope.Domain.Categories;
using SpendScope.Domain.ValueObjects;
using SpendScope.Features.Expenses.CreateExpense;

namespace SpendScope.UnitTests.Features;

/// <summary>Spec 15.8 handler logic against an in-memory IAppDbContext with the real query filters.</summary>
[TestFixture]
public sealed class CreateExpenseHandlerTests
{
    private readonly FakeCurrentUser _user = new(Guid.NewGuid());
    private TestDbContext _db = null!;
    private CreateExpenseHandler _handler = null!;
    private static readonly DateOnly Today = DateOnly.FromDateTime(FixedClock.Default);

    [SetUp]
    public void SetUp()
    {
        _db = TestDbContext.Create(_user);
        _db.Categories.AddRange(SystemCategories.All.Take(2));
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
        _handler = new CreateExpenseHandler(_db, new FakeUserAccounts("EUR"), _user, new FixedClock());
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public async Task Creates_the_expense_for_the_caller_in_their_currency()
    {
        var result = await _handler.HandleAsync(new CreateExpenseRequest(42.50m, Today, SystemCategories.FoodAndDining, "Lunch"), CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True, result.Error?.Message);
        Assert.Multiple(() =>
        {
            Assert.That(result.Value!.Currency, Is.EqualTo("EUR"), "copied from the user's default (spec 8.1)");
            Assert.That(result.Value.Category.Name, Is.EqualTo("Food and Dining"));
        });

        var stored = await _db.Expenses.SingleAsync();
        Assert.That(stored.UserId, Is.EqualTo(_user.UserId), "owner comes from the token, never the request");
    }

    [Test]
    public async Task Rejects_another_users_category_as_a_validation_error()
    {
        var otherUsersCategory = Category.CreateForUser(Guid.NewGuid(), "Private", new HexColor("#123456"), FixedClock.Default);
        _db.Categories.Add(otherUsersCategory);
        await _db.SaveChangesAsync();

        var result = await _handler.HandleAsync(new CreateExpenseRequest(5, Today, otherUsersCategory.Id, null), CancellationToken.None);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error!.Type, Is.EqualTo(ErrorType.Validation));
        Assert.That(result.Error.Field, Is.EqualTo("categoryId"));
        Assert.That(await _db.Expenses.IgnoreQueryFilters().CountAsync(), Is.Zero);
    }

    [Test]
    public async Task Unknown_category_is_rejected()
    {
        var result = await _handler.HandleAsync(new CreateExpenseRequest(5, Today, Guid.NewGuid(), null), CancellationToken.None);
        Assert.That(result.Error?.Code, Is.EqualTo("category_not_found"));
    }
}
