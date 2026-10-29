namespace Social.Services;

public class AppStateService
{
    public bool IsDrawerOpen { get; set; } = true;

    public void DrawerToggle()
    {
        IsDrawerOpen = !IsDrawerOpen;
    }
}
