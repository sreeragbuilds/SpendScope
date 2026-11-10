using System.ComponentModel.DataAnnotations;

namespace SpendScope.Application.Common;

/// <summary>Bound from the "Expenses" configuration section.</summary>
public sealed class ExpenseOptions
{
    public const string SectionName = "Expenses";

    /// <summary>CSV export refuses (422) when the filter matches more rows than this.</summary>
    [Range(1, 1_000_000)]
    public int MaxExportRows { get; set; } = 50_000;
}
