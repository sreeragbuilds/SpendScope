using MudBlazor;
using SpendScope.Web.Services.Browser;

namespace SpendScope.Web.Services;

/// <summary>Theme (spec 19.10) and the dark-mode preference, persisted in localStorage.</summary>
public sealed class ThemeState(BrowserStorage storage)
{
    private const string Key = "spendscope.theme";

    private static readonly string[] Sans = ["Instrument Sans", "system-ui", "sans-serif"];

    public static MudTheme Theme { get; } = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#635BFF",
            Secondary = "#425466",
            Tertiary = "#0570DE",
            Error = "#DF1B41",
            Background = "#F6F9FC",
            BackgroundGray = "#F6F9FC",
            Surface = "#FFFFFF",
            AppbarBackground = "#FFFFFF",
            AppbarText = "#0A2540",
            DrawerBackground = "#FFFFFF",
            DrawerText = "#0A2540",
            TextPrimary = "#0A2540",
            TextSecondary = "#5A6B80",
            ActionDefault = "#5A6B80",
            LinesDefault = "#E3E8EE",
            LinesInputs = "#8792A2",
            TableLines = "#E3E8EE",
            Divider = "#E3E8EE",
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#8A84FF",
            PrimaryContrastText = "#0B1829",
            Secondary = "#A3B3C8",
            Tertiary = "#4DA3FF",
            Error = "#FF6B86",
            ErrorContrastText = "#0B1829",
            Background = "#0B1829",
            BackgroundGray = "#0F1E33",
            Surface = "#13243B",
            AppbarBackground = "#0B1829",
            AppbarText = "#E6EBF1",
            DrawerBackground = "#0F1E33",
            DrawerText = "#E6EBF1",
            TextPrimary = "#E6EBF1",
            TextSecondary = "#A3B3C8",
            ActionDefault = "#A3B3C8",
            LinesDefault = "#27405E",
            LinesInputs = "#6E86A3",
            TableLines = "#27405E",
            Divider = "#27405E",
        },
        LayoutProperties = new LayoutProperties { DefaultBorderRadius = "6px" },
        Typography = new Typography
        {
            // One sans family throughout; hierarchy comes from size and weight (see app.css tokens).
            Default = new DefaultTypography { FontFamily = Sans },
            Body1 = new Body1Typography { FontFamily = Sans, FontSize = "1rem", LineHeight = "1.6" },
            Body2 = new Body2Typography { FontFamily = Sans, FontSize = "0.875rem", LineHeight = "1.55" },
            Button = new ButtonTypography { FontFamily = Sans, FontWeight = "600", TextTransform = "none" },
            Caption = new CaptionTypography { FontFamily = Sans, FontSize = "0.75rem" },
            H1 = new H1Typography { FontFamily = Sans, FontSize = "1.75rem", FontWeight = "600", LineHeight = "1.2", LetterSpacing = "-0.02em" },
            H2 = new H2Typography { FontFamily = Sans, FontSize = "1.5rem", FontWeight = "600", LineHeight = "1.2", LetterSpacing = "-0.02em" },
            H3 = new H3Typography { FontFamily = Sans, FontSize = "1.25rem", FontWeight = "600", LineHeight = "1.2" },
            H4 = new H4Typography { FontFamily = Sans, FontSize = "1.5rem", FontWeight = "600", LineHeight = "1.2", LetterSpacing = "-0.02em" },
            H5 = new H5Typography { FontFamily = Sans, FontSize = "1.25rem", FontWeight = "600", LineHeight = "1.2" },
            H6 = new H6Typography { FontFamily = Sans, FontSize = "1rem", FontWeight = "600", LineHeight = "1.2" },
        },
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
