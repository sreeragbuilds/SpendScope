using Bunit;
using Microsoft.Extensions.DependencyInjection;
using SpendScope.Web.Components;
using SpendScope.Web.Components.Charts;
using SpendScope.Web.Layout;
using SpendScope.Web.Services.Charts;

namespace SpendScope.Web.Tests;

public sealed class ComponentTests : WebTestContext
{
    [Fact]
    public async Task DoughnutChart_renders_the_slices_and_destroys_the_chart_on_removal()
    {
        var charts = new RecordingChartInterop(JSInterop.JSRuntime);
        Services.AddSingleton<ChartInterop>(charts);
        IReadOnlyList<ChartSlice> slices = [new("Groceries", 612.40m, "#84CC16"), new("Housing", 900m, "#8B5CF6")];

        var cut = Render<DoughnutChart>(p => p.Add(c => c.Data, slices).Add(c => c.CanvasId, "chart-test"));

        var (canvasId, rendered) = Assert.Single(charts.Doughnuts);
        Assert.Equal("chart-test", canvasId);
        Assert.Equal(["Groceries", "Housing"], rendered.Select(s => s.Label));
        Assert.Equal("img", cut.Find("canvas").GetAttribute("role"));

        await DisposeComponentsAsync();
        Assert.Equal(["chart-test"], charts.Destroyed);
    }

    [Fact]
    public void DoughnutChart_does_not_rerender_for_the_same_data()
    {
        var charts = new RecordingChartInterop(JSInterop.JSRuntime);
        Services.AddSingleton<ChartInterop>(charts);
        IReadOnlyList<ChartSlice> slices = [new("Other", 5m, "#6B7280")];

        var cut = Render<DoughnutChart>(p => p.Add(c => c.Data, slices));
        cut.Render();

        Assert.Single(charts.Doughnuts);
    }

    [Fact]
    public void SummaryCard_shows_label_value_and_caption()
    {
        var cut = Render<SummaryCard>(p => p
            .Add(c => c.Label, "Total spend")
            .Add(c => c.Value, "$1,842.75")
            .Add(c => c.Caption, "1 Mar 2026 – 31 Mar 2026"));

        Assert.Contains("Total spend", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("$1,842.75", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("1 Mar 2026", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void NavMenu_shows_the_admin_section_only_to_admins()
    {
        var auth = AddAuthorization();
        auth.SetAuthorized("Priya");

        var asUser = Render<NavMenu>();
        Assert.DoesNotContain("/admin/users", asUser.Markup, StringComparison.Ordinal);

        auth.SetRoles("Admin");
        var asAdmin = Render<NavMenu>();
        Assert.Contains("/admin/users", asAdmin.Markup, StringComparison.Ordinal);
        Assert.Contains("/admin/totals", asAdmin.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void CategoryBadge_always_shows_the_name_next_to_the_color()
    {
        var cut = Render<CategoryBadge>(p => p.Add(c => c.Name, "Groceries").Add(c => c.ColorHex, "#84CC16"));

        Assert.Contains("Groceries", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("#84CC16", cut.Find(".swatch").GetAttribute("style"), StringComparison.Ordinal);
    }
}
