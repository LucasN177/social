using Microsoft.JSInterop;
using MudBlazor;
using Social.Core.Interfaces.Services;

namespace Social.Services;

public class ThemeService(IJSRuntime jsRuntime) : IThemeService
{
    private const string StorageKey = "social.darkMode";

    public bool IsDarkMode { get; private set; }
    public MudTheme Theme { get; } = CreateTheme();
    public event Action? OnThemeChanged;

    /// <summary>
    /// Lädt die gespeicherte Wahl; ohne gespeicherten Wert gilt die Systemeinstellung.
    /// </summary>
    public async Task InitializeAsync(Func<Task<bool>> getSystemDarkMode)
    {
        var stored = await jsRuntime.InvokeAsync<string?>("localStorage.getItem", StorageKey);
        IsDarkMode = bool.TryParse(stored, out var darkMode) ? darkMode : await getSystemDarkMode();
        OnThemeChanged?.Invoke();
    }

    public async Task ToggleTheme()
    {
        IsDarkMode = !IsDarkMode;
        OnThemeChanged?.Invoke();
        await jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, IsDarkMode.ToString());
    }

    private static MudTheme CreateTheme()
    {
        return new MudTheme()
        {
            PaletteLight = new PaletteLight()
            {
                Primary = "#E91E63",
                PrimaryContrastText = "#FFFFFF",
                Secondary = "#FF5722",
                SecondaryContrastText = "#FFFFFF",
                Success = "#22C55E",
                Info = "#0284C7",
                Warning = "#F59E0B",
                Error = "#EF4444",
                Background = "#FAFAFA",
                Surface = "#FFFFFF",
                AppbarBackground = "#FFFFFF",
                AppbarText = "#212121",
                TextPrimary = "#212121",
                TextSecondary = "#757575",
                ActionDefault = "#757575",
                ActionDisabled = "#BDBDBD",
                ActionDisabledBackground = "#E0E0E0",
                Divider = "#E0E0E0",
            },
            PaletteDark = new PaletteDark()
            {
                Primary = "#F48FB1",
                PrimaryContrastText = "#0D1117",
                Secondary = "#FF8A65",
                SecondaryContrastText = "#0D1117",
                Success = "#4ADE80",
                Info = "#38BDF8",
                Warning = "#FBBF24",
                Error = "#F87171",
                Background = "#0D1117",
                Surface = "#161B22",
                AppbarBackground = "#161B22",
                AppbarText = "#F0F6FC",
                TextPrimary = "#F0F6FC",
                TextSecondary = "#8B949E",
                ActionDefault = "#8B949E",
                ActionDisabled = "#484F58",
                ActionDisabledBackground = "#21262D",
                Divider = "#30363D",
            },
            Typography = new Typography()
            {
                Default = new DefaultTypography()
                {
                    FontFamily = new[] { "Inter", "Roboto", "Helvetica", "Arial", "sans-serif" }
                },
                H4 = new H4Typography() { FontWeight = "700" },
                H5 = new H5Typography() { FontWeight = "600" },
                H6 = new H6Typography() { FontWeight = "600" },
                Button = new ButtonTypography()
                {
                    TextTransform = "none",
                    FontWeight = "600"
                }
            },
            LayoutProperties = new LayoutProperties()
            {
                DefaultBorderRadius = "12px"
            }
        };
    }
}
