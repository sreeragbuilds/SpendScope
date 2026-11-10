using MudBlazor;
using SpendScope.Web.Services.Browser;

namespace SpendScope.Web.Services;

/// <summary>Theme (spec 19.10) and the dark-mode preference, persisted in localStorage.</summary>
public sealed class ThemeState(BrowserStorage storage)
{
    private const string Key = "spendscope.theme";

    public static MudTheme Theme { get; } = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#0F766E",
            Secondary = "#B45309",
            Tertiary = "#F59E0B",
            Background = "#F8FAFC",
            BackgroundGray = "#EEF2F6",
            AppbarBackground = "#0F766E",
            DrawerBackground = "#FFFFFF",
            Surface = "#FFFFFF",
            TextPrimary = "#0F172A",
            TextSecondary = "#475569",
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#2DD4BF",
            Secondary = "#FBBF24",
            Tertiary = "#F59E0B",
            Background = "#0B1220",
            BackgroundGray = "#111A2B",
            Surface = "#121C2E",
            DrawerBackground = "#0E1726",
            AppbarBackground = "#0E1726",
            TextPrimary = "#E2E8F0",
            TextSecondary = "#94A3B8",
        },
        LayoutProperties = new LayoutProperties { DefaultBorderRadius = "10px" },
    };

    public bool IsDark { get; private set; }

    public event Action? Changed;

    public async Task InitializeAsync()
    {
        var stored = await storage.LocalGetAsync(Key);
        IsDark = stored is null ? await storage.PrefersDarkAsync() : stored == "dark";
        Changed?.Invoke();
    }

    public async Task ToggleAsync()
    {
        IsDark = !IsDark;
        await storage.LocalSetAsync(Key, IsDark ? "dark" : "light");
        Changed?.Invoke();
    }
}
