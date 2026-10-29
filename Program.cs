using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using Social;
using Supabase;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;
using Client = Supabase.Client;
using Social.Core.Interfaces.Infrastructure;
using Social.Core.Interfaces.Services;
using Social.Infrastructure;
using Social.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddMudServices(config =>
{
    // Unten mittig (Abstand zur Bottom-Navigation: wwwroot/css/app.css)
    config.SnackbarConfiguration.PositionClass = MudBlazor.Defaults.Classes.Position.BottomCenter;
    config.SnackbarConfiguration.PreventDuplicates = true;
    config.SnackbarConfiguration.VisibleStateDuration = 3000;
});

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

var url = builder.Configuration["Supabase:Url"];
var key = builder.Configuration["Supabase:AnonKey"];

if (!Uri.TryCreate(url, UriKind.Absolute, out _) || string.IsNullOrWhiteSpace(key))
{
    Console.Error.WriteLine(
        "Supabase ist nicht konfiguriert: In wwwroot/appsettings.json 'Supabase:Url' und 'Supabase:AnonKey' eintragen " +
        "(lokal) bzw. die Repository-Secrets SUPABASE_URL und SUPABASE_KEY setzen (Deployment).");
}

builder.Services.AddSingleton<IGotrueSessionPersistence<Session>, LocalStorageSessionHandler>();
builder.Services.AddSingleton<Client>(provider =>
{
    var client = new Client(
        url!, key, new SupabaseOptions
        {
            AutoRefreshToken = true,
            // Realtime-Client des Pakets setzt WebSocket-Header, das ist im Browser nicht möglich (PlatformNotSupported)
            AutoConnectRealtime = false
        }
    );
    // SupabaseOptions.SessionHandler wird in Supabase 1.1.1 nicht an Gotrue durchgereicht, daher explizit setzen
    client.Auth.SetPersistence(provider.GetRequiredService<IGotrueSessionPersistence<Session>>());
    return client;
});
builder.Services.AddSingleton<IAuthRepository, AuthRepository>();
builder.Services.AddSingleton<IUserRepository, UserRepository>();
builder.Services.AddSingleton<IPostRepository, PostRepository>();
builder.Services.AddSingleton<IStoryRepository, StoryRepository>();

builder.Services.AddSingleton<AppStateService>();
builder.Services.AddSingleton<IAuthService, AuthService>();
builder.Services.AddSingleton<IUserService, UserService>();
builder.Services.AddSingleton<IPostService, PostService>();
builder.Services.AddSingleton<IStoryService, StoryService>();
builder.Services.AddSingleton<IThemeService, ThemeService>();
builder.Services.AddSingleton<INotificationService, NotificationService>();

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddSingleton<AuthenticationStateProvider, SupabaseAuthStateProvider>();

var host = builder.Build();

// Gespeicherte Session laden und gegen den Server prüfen bzw. Token erneuern, bevor die erste Seite gerendert wird.
// Ist die Session ungültig, verwirft Gotrue sie und der User landet auf dem Login.
try
{
    var supabase = host.Services.GetRequiredService<Client>();
    supabase.Auth.LoadSession();
    await supabase.Auth.RetrieveSessionAsync();
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Supabase initialization failed: {ex}");
}

await host.RunAsync();
