using SpendScope.Domain.Auth;
using SpendScope.Domain.Categories;
using SpendScope.Domain.Common;
using SpendScope.Domain.ValueObjects;

namespace SpendScope.UnitTests.Domain;

[TestFixture]
public sealed class MoneyTests
{
    [Test]
    public void Equality_uses_amount_and_currency()
    {
        var five = new Money(5, "USD");
        var alsoFive = new Money(5, "USD");
        Assert.That(five, Is.EqualTo(alsoFive));
        Assert.That(five, Is.Not.EqualTo(new Money(5, "EUR")));
    }

    [Test]
    public void Adding_the_same_currency_sums() => Assert.That((new Money(1.25m, "USD") + new Money(2, "USD")).Amount, Is.EqualTo(3.25m));

    [Test]
    public void Adding_different_currencies_throws() =>
        Assert.That(() => new Money(1, "USD") + new Money(1, "EUR"), Throws.TypeOf<DomainException>());

    [TestCase("usd")]
    [TestCase("US")]
    [TestCase("")]
    [TestCase("U5D")]
    public void Rejects_invalid_currency_codes(string code) =>
        Assert.That(() => new Money(1, code), Throws.TypeOf<DomainException>());
}

[TestFixture]
public sealed class HexColorAndEmailTests
{
    [TestCase("#F59E0B", true)]
    [TestCase("#abcdef", true)]
    [TestCase("F59E0B", false)]
    [TestCase("#F59E0", false)]
    [TestCase("#GGGGGG", false)]
    [TestCase("red", false)]
    [TestCase(null, false)]
    public void HexColor_validation(string? value, bool valid) => Assert.That(HexColor.IsValid(value), Is.EqualTo(valid));

    [Test]
    public void HexColor_is_stored_uppercase() => Assert.That(new HexColor("#abcdef").Value, Is.EqualTo("#ABCDEF"));

    [TestCase("priya@example.com", true)]
    [TestCase("a.b+c@sub.example.co", true)]
    [TestCase("no-at-sign.example.com", false)]
    [TestCase("two@@example.com", false)]
    [TestCase("trailing@example.", false)]
    [TestCase("spa ce@example.com", false)]
    [TestCase("", false)]
    public void Email_validation(string value, bool valid) => Assert.That(EmailAddress.IsValid(value), Is.EqualTo(valid));
}

/// <summary>FR-CAT-02, 03 and 06.</summary>
[TestFixture]
public sealed class CategoryTests
{
    [Test]
    public void User_category_is_trimmed_normalized_and_owned()
    {
        var owner = Guid.NewGuid();
        var category = Category.CreateForUser(owner, "  Coffee Runs ", new HexColor("#7c2d12"), FixedClock.Default);

        Assert.Multiple(() =>
        {
            Assert.That(category.Name, Is.EqualTo("Coffee Runs"));
            Assert.That(category.NormalizedName, Is.EqualTo("COFFEE RUNS"));
            Assert.That(category.ColorHex, Is.EqualTo("#7C2D12"));
            Assert.That(category.IsSystem, Is.False);
            Assert.That(category.IsOwnedBy(owner), Is.True);
            Assert.That(category.IsOwnedBy(Guid.NewGuid()), Is.False);
        });
    }

    [Test]
    public void System_categories_cannot_be_renamed_or_recolored()
    {
        var groceries = SystemCategories.All.Single(c => c.Name == "Groceries");

        Assert.That(() => groceries.Rename("Food"), Throws.TypeOf<DomainException>().With.Property("Code").EqualTo("system_category_read_only"));
        Assert.That(() => groceries.Recolor(new HexColor("#000000")), Throws.TypeOf<DomainException>());
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("This category name is far too long to be accepted")]
    public void Rejects_invalid_names(string name) =>
        Assert.That(() => Category.CreateForUser(Guid.NewGuid(), name, new HexColor("#000000"), FixedClock.Default), Throws.TypeOf<DomainException>());

    [Test]
    public void Ten_system_categories_with_unique_names_and_ids()
    {
        Assert.That(SystemCategories.All, Has.Count.EqualTo(10));
        Assert.That(SystemCategories.All.Select(c => c.Id), Is.Unique);
        Assert.That(SystemCategories.All.Select(c => c.NormalizedName), Is.Unique);
        Assert.That(SystemCategories.All, Has.All.Property(nameof(Category.IsSystem)).True);
    }
}

/// <summary>Spec 13.5: refresh token lifecycle.</summary>
[TestFixture]
public sealed class RefreshTokenTests
{
    private static readonly DateTime Now = FixedClock.Default;

    [Test]
    public void New_token_is_active_until_it_expires()
    {
        var token = RefreshToken.Issue(Guid.NewGuid(), "hash", Now, TimeSpan.FromDays(14), "203.0.113.7");

        Assert.That(token.IsActive(Now), Is.True);
        Assert.That(token.IsActive(Now.AddDays(14).AddSeconds(-1)), Is.True);
        Assert.That(token.IsActive(Now.AddDays(14)), Is.False);
    }

    [Test]
    public void Revoke_is_idempotent_and_keeps_the_first_replacement()
    {
        var token = RefreshToken.Issue(Guid.NewGuid(), "hash", Now, TimeSpan.FromDays(14), null);

        token.Revoke(Now.AddMinutes(1), "next-hash");
        token.Revoke(Now.AddMinutes(5), "other-hash");

        Assert.Multiple(() =>
        {
            Assert.That(token.IsActive(Now.AddMinutes(2)), Is.False);
            Assert.That(token.RevokedAtUtc, Is.EqualTo(Now.AddMinutes(1)));
            Assert.That(token.ReplacedByTokenHash, Is.EqualTo("next-hash"));
        });
    }

    [Test]
    public void Long_ip_addresses_are_truncated_to_the_column_size()
    {
        var token = RefreshToken.Issue(Guid.NewGuid(), "hash", Now, TimeSpan.FromDays(1), new string('a', 60));
        Assert.That(token.CreatedByIp, Has.Length.EqualTo(45));
    }
}
