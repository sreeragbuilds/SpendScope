using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SpendScope.Application.Abstractions;
using SpendScope.Application.Common;
using SpendScope.Contracts.Expenses;
using SpendScope.Domain.Common;
using SpendScope.Features.Common;

namespace SpendScope.Features.Expenses.ExportExpensesCsv;

/// <summary>Spec 15.13. Same filters as the list, no paging, capped. FR-EXP-11.</summary>
public sealed record CsvExport(string FileName, IAsyncEnumerable<ExpenseDto> Rows);

public sealed class ExportExpensesCsvHandler(IAppDbContext db, IClock clock, IOptions<ExpenseOptions> options)
{
    public async Task<Result<CsvExport>> HandleAsync(ExpenseQuery query, CancellationToken ct)
    {
        if (query.CategoryId is { } categoryId && !await db.Categories.AnyAsync(c => c.Id == categoryId, ct))
        {
            return Error.Validation("category_not_found", "Category does not exist.", "categoryId");
        }

        var range = DateRange.Resolve(query.From, query.To, clock.Today);
        var filtered = db.Expenses.AsNoTracking().ApplyFilters(query, range);

        var max = options.Value.MaxExportRows;
        if (await filtered.CountAsync(ct) > max)
        {
            return Error.Unprocessable("export_too_large", $"The export is limited to {max:N0} rows. Narrow the filters.");
        }

        var rows = filtered.ApplySort(query).Select(ExpenseQueries.ToDto).AsAsyncEnumerable();
        var fileName = string.Create(CultureInfo.InvariantCulture, $"spendscope-{range.From:yyyy-MM-dd}-to-{range.To:yyyy-MM-dd}.csv");
        return new CsvExport(fileName, rows);
    }
}

/// <summary>CSV writing with formula-injection protection (OWASP "CSV injection").</summary>
public static class Csv
{
    public const string Header = "Date,Category,Amount,Currency,Note";

    // Cells starting with these are executed as formulas by Excel, LibreOffice and Sheets.
    private static readonly char[] FormulaTriggers = ['=', '+', '-', '@', '\t', '\r'];

    public static string Row(ExpenseDto e) => string.Join(
        ',',
        e.OccurredOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        Escape(e.Category.Name),
        e.Amount.ToString("0.00", CultureInfo.InvariantCulture),
        e.Currency,
        Escape(e.Note));

    /// <summary>
    /// Prefixes formula-looking text with a single quote so spreadsheets show it as text, then
    /// quotes the cell if it contains a delimiter, quote or line break.
    /// </summary>
    public static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (FormulaTriggers.Contains(value[0]))
        {
            value = "'" + value;
        }

        return value.AsSpan().IndexOfAny(",\"\r\n") >= 0
            ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : value;
    }

    public static async Task WriteAsync(Stream output, IAsyncEnumerable<ExpenseDto> rows, CancellationToken ct)
    {
        await using var writer = new StreamWriter(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), leaveOpen: true);
        await writer.WriteLineAsync(Header.AsMemory(), ct);
        await foreach (var row in rows.WithCancellation(ct))
        {
            await writer.WriteLineAsync(Row(row).AsMemory(), ct);
        }
    }
}

public sealed class ExportExpensesCsvEndpoint : IFeatureEndpoint
{
    public void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/expenses/export", async ([AsParameters] ExpenseQuery query, ExportExpensesCsvHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(query, ct)).ToHttpResult(export => Results.Stream(
                    stream => Csv.WriteAsync(stream, export.Rows, ct),
                    contentType: "text/csv; charset=utf-8",
                    fileDownloadName: export.FileName)))
            .WithValidation<ExpenseQuery>()
            .RequireAuthorization(Policies.RequireUser)
            .WithTags("Expenses")
            .WithName("ExportExpensesCsv")
            .Produces(StatusCodes.Status200OK, contentType: "text/csv")
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
}
