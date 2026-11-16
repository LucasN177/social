using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Social.Services;

/// <summary>
/// Sprache der App (de/en). Die Kultur wird beim Start in Program.cs gesetzt; ein Wechsel
/// speichert die Wahl im Browser und lädt die App neu (Blazor WASM kann die Kultur
/// zur Laufzeit nicht für alle Komponenten umstellen).
/// </summary>
public class LanguageService(IJSRuntime js, NavigationManager navigation)
{
    public static readonly IReadOnlyList<(string Code, string Name)> Available =
    [
        ("en", "English"),
        ("de", "Deutsch")
    ];

    public const string Default = "en";

    public string Current => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName is "de" ? "de" : "en";

    /// <summary>Gespeicherte Wahl oder Browsersprache (index.html: socialI18n.getLanguage).</summary>
    public static async Task<string> ApplyStoredCultureAsync(IJSRuntime js)
    {
        string code;
        try
        {
            code = await js.InvokeAsync<string>("socialI18n.getLanguage");
        }
        catch (JSException)
        {
            code = Default;
        }

        if (Available.All(l => l.Code != code)) code = Default;
        var culture = CultureInfo.GetCultureInfo(code == "de" ? "de-DE" : "en-US");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        return code;
    }

    public async Task SetAsync(string code)
    {
        if (code == Current || Available.All(l => l.Code != code)) return;
        await js.InvokeVoidAsync("socialI18n.setLanguage", code);
        navigation.NavigateTo(navigation.Uri, forceLoad: true);
    }
}
