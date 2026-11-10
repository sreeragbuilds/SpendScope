using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using SpendScope.Application.Common;
using SpendScope.Contracts.Expenses;
using SpendScope.Features.Common;
using SpendScope.Features.Expenses.ExportExpensesCsv;
using SpendScope.Features.Reports.GetMonthlyTotals;

namespace SpendScope.UnitTests.Features;

/// <summary>Spec 15.13: CSV formula injection.</summary>
[TestFixture]
public sealed class CsvTests
{
    [TestCase("=HYPERLINK(\"http://evil\")", "\"'=HYPERLINK(\"\"http://evil\"\")\"")]
    [TestCase("+1+1", "'+1+1")]
    [TestCase("-2+3", "'-2+3")]
    [TestCase("@SUM(A1)", "'@SUM(A1)")]
    [TestCase("\tcmd", "'\tcmd")]
    [TestCase("Lunch, with team", "\"Lunch, with team\"")]
    [TestCase("Line\nbreak", "\"Line\nbreak\"")]
    [TestCase("Plain note", "Plain note")]
    [TestCase("", "")]
    [TestCase(null, "")]
    public void Escape(string? input, string expected) => Assert.That(Csv.Escape(input), Is.EqualTo(expected));

    [Test]
    public void Row_uses_invariant_formats()
    {
        var row = Csv.Row(new ExpenseDto(
            Guid.NewGuid(), 1234.5m, "EUR", new DateOnly(2026, 3, 14), new CategoryRef(Guid.NewGuid(), "Food and Dining", "#F59E0B"), "=cmd", DateTime.UtcNow, null));

        Assert.That(row, Is.EqualTo("2026-03-14,Food and Dining,1234.50,EUR,'=cmd"));
    }

    [Test]
    public async Task WriteAsync_writes_header_then_rows()
    {
        static async IAsyncEnumerable<ExpenseDto> Rows()
        {
            yield return new ExpenseDto(Guid.NewGuid(), 5m, "USD", new DateOnly(2026, 1, 2), new CategoryRef(Guid.NewGuid(), "Other", "#6B7280"), null, DateTime.UtcNow, null);
            await Task.CompletedTask;
        }

        using var stream = new MemoryStream();
        await Csv.WriteAsync(stream, Rows(), CancellationToken.None);
        var lines = System.Text.Encoding.UTF8.GetString(stream.ToArray()).TrimStart('﻿').Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.That(lines, Is.EqualTo(new[] { Csv.Header, "2026-01-02,Other,5.00,USD," }));
    }
}

[TestFixture]
public sealed class ReportMathAndRangeTests
{
    [TestCase(612.40, 1842.75, 33.23)]
    [TestCase(900, 1842.75, 48.84)]
    [TestCase(5, 0, 0)]
    [TestCase(0, 0, 0)]
    public void Percent_is_rounded_to_two_decimals(decimal part, decimal total, decimal expected) =>
        Assert.That(ReportMath.Percent(part, total), Is.EqualTo(expected));

    [Test]
    public void Average_is_zero_for_no_expenses() => Assert.That(ReportMath.Average(0, 0), Is.Zero);

    [Test]
    public void Average_is_rounded() => Assert.That(ReportMath.Average(1842.75m, 46), Is.EqualTo(40.06m));

    [Test]
    public void Monthly_zero_fill_always_returns_twelve_months()
    {
        var months = GetMonthlyTotalsHandler.ZeroFill(new Dictionary<int, (decimal, int)> { [3] = (1842.75m, 46) });

        Assert.That(months, Has.Count.EqualTo(12));
        Assert.That(months.Select(m => m.Month), Is.EqualTo(Enumerable.Range(1, 12)));
        Assert.That(months[2].Total, Is.EqualTo(1842.75m));
        Assert.That(months.Where(m => m.Month != 3).Sum(m => m.Total), Is.Zero);
    }

    [Test]
    public void DateRange_defaults_to_the_whole_current_month()
    {
        var range = DateRange.Resolve(null, null, new DateOnly(2026, 2, 10));
        Assert.That(range, Is.EqualTo(new DateRange(new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28))));
    }

    [Test]
    public void DateRange_with_only_to_starts_on_that_month()
    {
        var range = DateRange.Resolve(null, new DateOnly(2025, 12, 15), new DateOnly(2026, 2, 10));
        Assert.That(range.From, Is.EqualTo(new DateOnly(2025, 12, 1)));
        Assert.That(range.Days, Is.EqualTo(15));
    }
}

/// <summary>Spec 10.6: ErrorType to HTTP status mapping.</summary>
[TestFixture]
public sealed class ResultHttpTests
{
    [TestCase(ErrorType.NotFound, 404)]
    [TestCase(ErrorType.Conflict, 409)]
    [TestCase(ErrorType.Unauthorized, 401)]
    [TestCase(ErrorType.Forbidden, 403)]
    [TestCase(ErrorType.Locked, 423)]
    [TestCase(ErrorType.Unprocessable, 422)]
    [TestCase(ErrorType.Failure, 500)]
    public void Maps_error_types_to_status_codes(ErrorType type, int status)
    {
        var result = new Error("some_code", "Message", type).ToProblem();

        var problem = result as ProblemHttpResult;
        Assert.That(problem, Is.Not.Null, result.GetType().Name);
        Assert.That(problem!.StatusCode, Is.EqualTo(status));
        Assert.That(problem.ProblemDetails.Extensions["errorCode"], Is.EqualTo("some_code"));
    }

    [Test]
    public void Validation_errors_become_ValidationProblem_keyed_by_field()
    {
        var result = Error.Validation("category_not_found", "Category does not exist.", "categoryId").ToProblem();

        var problem = result as ProblemHttpResult;
        Assert.That(problem, Is.Not.Null);
        Assert.That(problem!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
        var details = (HttpValidationProblemDetails)problem.ProblemDetails;
        Assert.That(details.Errors["categoryId"], Is.EqualTo(new[] { "Category does not exist." }));
    }

    [Test]
    public async Task Locked_adds_a_Retry_After_header()
    {
        var result = Error.Locked("account_locked", "Locked.", TimeSpan.FromSeconds(89.2)).ToProblem();
        var context = new DefaultHttpContext { RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider() };
        context.Response.Body = new MemoryStream();

        await result.ExecuteAsync(context);

        Assert.That(context.Response.Headers.RetryAfter.ToString(), Is.EqualTo("90"));
        Assert.That(context.Response.StatusCode, Is.EqualTo(423));
    }

    [Test]
    public void Success_uses_the_supplied_result()
    {
        var ok = Result<int>.Ok(5).ToHttpResult(v => Results.Ok(v * 2));
        Assert.That(ok, Is.InstanceOf<Ok<int>>());
        Assert.That(((Ok<int>)ok).Value, Is.EqualTo(10));
    }
}
