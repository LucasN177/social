using MudBlazor;

namespace Social.Core.Interfaces.Services;

public interface IThemeService
{
    bool IsDarkMode { get; }
    MudTheme Theme { get; }
    event Action? OnThemeChanged;
    Task InitializeAsync(Func<Task<bool>> getSystemDarkMode);
    Task ToggleTheme();
}
