using Microsoft.JSInterop;

namespace SpendScope.Web.Services.Charts;

public sealed record ChartSlice(string Label, decimal Value, string ColorHex);

/// <summary>
/// Spec 19.7. Builds Chart.js configs in C#; wwwroot/js/charts.js adds the formatter
/// callbacks that cannot be serialized.
/// </summary>
public class ChartInterop(IJSRuntime js)
{
    public virtual ValueTask RenderDoughnutAsync(string canvasId, IReadOnlyList<ChartSlice> slices, string currency) =>
        RenderAsync(canvasId, new
        {
            type = "doughnut",
            currency,
            data = new
            {
                labels = slices.Select(s => s.Label),
                datasets = new[]
                {
                    new
                    {
                        data = slices.Select(s => s.Value),
                        backgroundColor = slices.Select(s => s.ColorHex),
                        borderWidth = 2,
                    },
                },
            },
            options = new
            {
                responsive = true,
                maintainAspectRatio = true,
                cutout = "62%",
                // The page shows its own legend table with amounts and percentages.
                plugins = new { legend = new { display = false } },
            },
        });

    public virtual ValueTask RenderBarAsync(string canvasId, IReadOnlyList<string> labels, IReadOnlyList<decimal> values, string colorHex, string currency) =>
        RenderAsync(canvasId, new
        {
            type = "bar",
            currency,
            compactTicks = true,
            data = new
            {
                labels,
                datasets = new[] { new { label = "Total", data = values, backgroundColor = colorHex, borderRadius = 6, maxBarThickness = 36 } },
            },
            options = new
            {
                responsive = true,
                maintainAspectRatio = false,
                plugins = new { legend = new { display = false } },
                scales = new { y = new { beginAtZero = true }, x = new { grid = new { display = false } } },
            },
        });

    public virtual ValueTask DestroyAsync(string canvasId) => js.InvokeVoidAsync("spendscopeCharts.destroy", canvasId);

    private async ValueTask RenderAsync(string canvasId, object config) =>
        await js.InvokeAsync<bool>("spendscopeCharts.render", canvasId, config);
}
