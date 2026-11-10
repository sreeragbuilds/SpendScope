using FluentValidation;
using SpendScope.Contracts.Auth;
using SpendScope.Contracts.Categories;
using SpendScope.Contracts.Expenses;
using SpendScope.Features.Auth.GetProfile;
using SpendScope.Features.Auth.Register;
using SpendScope.Features.Categories.CreateCategory;
using SpendScope.Features.Expenses;
using SpendScope.Features.Expenses.CreateExpense;
using SpendScope.Features.Reports.GetCategorySummary;
using SpendScope.Features.Reports.GetMonthlyTotals;

namespace SpendScope.UnitTests.Features;

/// <summary>Spec section 17 rules and their error codes.</summary>
[TestFixture]
public sealed class ValidatorTests
{
    private static readonly FixedClock Clock = new();
    private static readonly DateOnly Today = DateOnly.FromDateTime(FixedClock.Default);

    private static string[] Codes<T>(IValidator<T> validator, T request, string field) =>
        validator.Validate(request).Errors
            .Where(e => string.Equals(e.PropertyName, field, StringComparison.OrdinalIgnoreCase))
            .Select(e => e.ErrorCode)
            .ToArray();

    // ---------- Expenses ----------

    [Test]
    public void CreateExpense_valid_request_passes() =>
        Assert.That(new CreateExpenseValidator(Clock).Validate(new CreateExpenseRequest(42.50m, Today, Guid.NewGuid(), "ok")).IsValid, Is.True);

    [TestCase(0, "amount_range")]
    [TestCase(-3, "amount_range")]
    [TestCase(1_000_000.01, "amount_range")]
    [TestCase(1.005, "amount_precision")]
    public void CreateExpense_amount_rules(decimal amount, string code) =>
        Assert.That(Codes(new CreateExpenseValidator(Clock), new CreateExpenseRequest(amount, Today, Guid.NewGuid(), null), "Amount"), Does.Contain(code));

    [Test]
    public void CreateExpense_date_more_than_a_year_ahead_is_rejected() =>
        Assert.That(
            Codes(new CreateExpenseValidator(Clock), new CreateExpenseRequest(1, Today.AddDays(366), Guid.NewGuid(), null), "OccurredOn"),
            Does.Contain("date_future"));

    [Test]
    public void CreateExpense_note_and_category_rules()
    {
        var validator = new CreateExpenseValidator(Clock);
        Assert.That(Codes(validator, new CreateExpenseRequest(1, Today, Guid.NewGuid(), new string('n', 501)), "Note"), Does.Contain("note_length"));
        Assert.That(Codes(validator, new CreateExpenseRequest(1, Today, Guid.NewGuid(), "bell\u0007"), "Note"), Does.Contain("note_invalid"));
        Assert.That(Codes(validator, new CreateExpenseRequest(1, Today, Guid.NewGuid(), "line one\nline two"), "Note"), Is.Empty, "newlines are allowed");
        Assert.That(Codes(validator, new CreateExpenseRequest(1, Today, Guid.Empty, null), "CategoryId"), Does.Contain("category_required"));
    }

    [Test]
    public void ExpenseQuery_rejects_bad_ranges_sorts_and_paging()
    {
        var validator = new ExpenseQueryValidator(Clock);

        Assert.That(Codes(validator, new ExpenseQuery { From = Today, To = Today.AddDays(-1) }, "from"), Does.Contain("range_invalid"));
        Assert.That(Codes(validator, new ExpenseQuery { From = Today.AddDays(-400), To = Today }, "to"), Does.Contain("range_too_large"));
        Assert.That(Codes(validator, new ExpenseQuery { SortBy = "password" }, "SortBy"), Does.Contain("sort_invalid"));
        Assert.That(Codes(validator, new ExpenseQuery { SortDir = "sideways" }, "SortDir"), Does.Contain("sort_invalid"));
        Assert.That(Codes(validator, new ExpenseQuery { Page = 0 }, "Page"), Does.Contain("page_invalid"));
        Assert.That(Codes(validator, new ExpenseQuery { PageSize = 101 }, "PageSize"), Does.Contain("page_size_invalid"));
        Assert.That(Codes(validator, new ExpenseQuery { MinAmount = 10, MaxAmount = 5 }, "minAmount"), Does.Contain("amount_range"));
    }

    [Test]
    public void ExpenseQuery_with_no_parameters_is_valid() =>
        Assert.That(new ExpenseQueryValidator(Clock).Validate(new ExpenseQuery()).IsValid, Is.True);

    [Test]
    public void ExpenseQuery_accepts_a_366_day_range() =>
        Assert.That(new ExpenseQueryValidator(Clock).Validate(new ExpenseQuery { From = Today.AddDays(-365), To = Today }).IsValid, Is.True);

    // ---------- Auth ----------

    [TestCase("Short1!A", Description = "too short")]
    [TestCase("alllowercase1!", Description = "no uppercase")]
    [TestCase("ALLUPPERCASE1!", Description = "no lowercase")]
    [TestCase("NoDigitsHere!", Description = "no digit")]
    [TestCase("NoSymbols1234", Description = "no symbol")]
    public void Register_rejects_passwords_that_break_the_policy(string password) =>
        Assert.That(
            Codes(new RegisterValidator(), new RegisterRequest("priya@example.com", password, "Priya"), "Password"),
            Does.Contain("password_policy"));

    [TestCase("Str0ng!Passphrase")]
    [TestCase("Aa1!Aa1!Aa1!", Description = "exactly 4 unique characters is enough")]
    public void Register_accepts_passwords_that_meet_the_policy(string password) =>
        Assert.That(Codes(new RegisterValidator(), new RegisterRequest("priya@example.com", password, "Priya"), "Password"), Is.Empty);

    [Test]
    public void Unique_character_rule_is_reported_on_its_own()
    {
        // With upper, lower, digit and symbol all required, 4 unique characters is implied; the
        // rule only matters for the live checklist, so check its verdict directly.
        var rule = PasswordPolicy.Evaluate("aaaaaaaaaa").Single(r => r.Rule.Contains("different characters", StringComparison.Ordinal));
        Assert.That(rule.Met, Is.False);
    }

    [TestCase("P", "display_name_length")]
    [TestCase("  ", "display_name_length")]
    public void Register_display_name_rules(string name, string code) =>
        Assert.That(Codes(new RegisterValidator(), new RegisterRequest("priya@example.com", "Str0ng!Passphrase", name), "DisplayName"), Does.Contain(code));

    [Test]
    public void Register_rejects_bad_email() =>
        Assert.That(Codes(new RegisterValidator(), new RegisterRequest("not-an-email", "Str0ng!Passphrase", "Priya"), "Email"), Does.Contain("email_invalid"));

    [TestCase("USD", true)]
    [TestCase("INR", true)]
    [TestCase("BTC", false)]
    [TestCase("usd", false)]
    public void Profile_currency_must_be_supported(string currency, bool valid) =>
        Assert.That(new UpdateProfileValidator().Validate(new UpdateProfileRequest("Priya", currency)).IsValid, Is.EqualTo(valid));

    // ---------- Categories and reports ----------

    [Test]
    public void Category_name_and_color_rules()
    {
        var validator = new CreateCategoryValidator();
        Assert.That(Codes(validator, new CreateCategoryRequest("", "#112233"), "Name"), Does.Contain("category_name_length"));
        Assert.That(Codes(validator, new CreateCategoryRequest(new string('c', 41), "#112233"), "Name"), Does.Contain("category_name_length"));
        Assert.That(Codes(validator, new CreateCategoryRequest("Coffee", "#11223"), "ColorHex"), Does.Contain("color_invalid"));
        Assert.That(validator.Validate(new CreateCategoryRequest("Coffee", "#7C2D12")).IsValid, Is.True);
    }

    [Test]
    public void Summary_range_over_366_days_is_rejected() =>
        Assert.That(
            Codes(new SummaryQueryValidator(Clock), new SummaryQuery(Today.AddYears(-2), Today), "to"),
            Does.Contain("range_too_large"));

    [TestCase(1999, false)]
    [TestCase(2000, true)]
    [TestCase(2026, true)]
    [TestCase(2027, false)]
    public void Monthly_year_must_be_2000_to_current(int year, bool valid) =>
        Assert.That(new MonthlyQueryValidator(Clock).Validate(new MonthlyQuery(year)).IsValid, Is.EqualTo(valid));
}
