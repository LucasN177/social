using MudBlazor;
using Social.Core.Interfaces.Services;
using Social.Interfaces;

namespace Social.Ui;



    public class ThemeService : IThemeService
    {
        public bool IsDarkMode { get; private set; } = false;
        public MudTheme Theme { get; set; }
        public event Action OnThemeChanged;

        public ThemeService()
        {
            Theme = CreateTheme();
        }

        public void ToggleTheme()
        {
            IsDarkMode = !IsDarkMode;
            OnThemeChanged?.Invoke();
        }

        private MudTheme CreateTheme()
        {
            return new MudTheme()
            {
                PaletteLight = new PaletteLight()
                {
                    Primary = "#E91E63",
                    Secondary = "#FF5722",
                    Background = "#FAFAFA",
                    Surface = "#FFFFFF",
                    AppbarBackground = "#FFFFFF",
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
                    Secondary = "#FF8A65",
                    Background = "#0D1117",
                    Surface = "#161B22",
                    AppbarBackground = "#161B22",
                    TextPrimary = "#F0F6FC",
                    TextSecondary = "#8B949E",
                    ActionDefault = "#8B949E",
                    ActionDisabled = "#484F58",
                    ActionDisabledBackground = "#21262D",
                    Divider = "#30363D",
                }
            };
        }
    }
