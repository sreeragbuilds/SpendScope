using System.Globalization;
using SpendScope.Web.Components;
using SpendScope.Web.Services;
using SpendScope.Web.Services.Api;
using SpendScope.Web.Services.Auth;

namespace SpendScope.Web.Tests;

public sealed class ServiceTests
{
    [Fact]
    public void JwtClaims_reads_name_subject_and_every_role()
    {
        var id = Guid.NewGuid();
        var token = $"e30.{JwtClaims.Base64UrlJson(new { sub = id.ToString(), name = "Priya", role = new[] { "User", "Admin" } })}.sig";

        var user = JwtClaims.ToPrincipal(token);

        Assert.True(user.Identity!.IsAuthenticated);
        Assert.Equal("Priya", user.Identity.Name);
        Assert.Equal(id, user.UserId());
        Assert.True(user.IsInRole("Admin"));
        Assert.True(user.IsInRole("User"));
    }

    [Fact]
    public void JwtClaims_handles_a_single_role_string() =>
        Assert.True(JwtClaims.ToPrincipal($"e30.{JwtClaims.Base64UrlJson(new { sub = "x", role = "User" })}.sig").IsInRole("User"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-jwt")]
    public void JwtClaims_without_a_token_is_anonymous(string? token) =>
        Assert.False(JwtClaims.ToPrincipal(token).Identity!.IsAuthenticated);

    [Fact]
    public void DateRange_presets()
    {
        var today = new DateOnly(2026, 3, 14);

        Assert.Equal((new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31)), DateRangePicker.PresetRange("month", today));
        Assert.Equal((new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31)), DateRangePicker.PresetRange("3months", today));
        Assert.Equal((new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)), DateRangePicker.PresetRange("year", today));
        Assert.Equal("3months", DateRangePicker.DetectPreset(new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31), today));
        Assert.Equal("custom", DateRangePicker.DetectPreset(new DateOnly(2026, 1, 5), new DateOnly(2026, 3, 31), today));
    }

    [Fact]
    public void DateRange_rejects_from_after_to_and_ranges_over_366_days()
    {
        Assert.NotNull(DateRangePicker.Validate(new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 1)));
        Assert.NotNull(DateRangePicker.Validate(new DateOnly(2025, 1, 1), new DateOnly(2026, 3, 1)));
        Assert.Null(DateRangePicker.Validate(new DateOnly(2025, 3, 1), new DateOnly(2026, 3, 1)));
    }

    [Fact]
    public void Money_formatting_uses_the_currency_symbol_and_handles_zero()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        try
        {
            Assert.Equal("$1,842.75", Format.Money(1842.75m, "USD"));
            Assert.Equal("$0.00", Format.Money(0, "USD"));
            Assert.Equal("₹42.50", Format.Money(42.5m, "INR"));
            Assert.Equal("¥1,235", Format.Money(1234.5m, "JPY"));
            Assert.Equal("14 Mar 2026", Format.Date(new DateOnly(2026, 3, 14)));
            Assert.Equal("33.2%", Format.Percent(33.23m));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Status_messages_follow_the_spec_table()
    {
        Assert.Equal("Can't reach SpendScope. Check your connection.", ApiMessages.For(ApiResult<Unit>.NetworkFailure()));
        Assert.Equal("Too many requests. Wait a moment.", ApiMessages.For(ApiResult<Unit>.Fail(429, null)));
        Assert.Equal("You don't have permission to do that.", ApiMessages.For(ApiResult<Unit>.Fail(403, null)));
        Assert.Contains("Reference: abc", ApiMessages.For(ApiResult<Unit>.Fail(500, new Problem { TraceId = "abc" })), StringComparison.Ordinal);
        Assert.Equal("Account temporarily locked. Try again later.", ApiMessages.LockedMessage(null));
    }
}
