using Microsoft.Extensions.Localization;

namespace Social.Resources;

/// <summary>
/// Marker für die gemeinsamen UI-Texte. Schlüssel ist der englische Text – fehlt eine
/// Übersetzung, zeigt IStringLocalizer den Schlüssel, Englisch braucht also keine eigene Datei.
/// Übersetzungen: SharedResource.de.resx. Texte mit Platzhaltern: L["Reply sent to {0}", name].
/// </summary>
public class SharedResource;

/// <summary>Für statische Helfer (z. B. PostCard.TimeAgo), die keinen injizierten Localizer haben.</summary>
public static class Loc
{
    private static IStringLocalizer<SharedResource>? _localizer;

    /// <summary>Wird in Program.cs gesetzt. Ohne Initialisierung (z. B. in Tests) kommt der Schlüssel zurück.</summary>
    public static IStringLocalizer<SharedResource> L
    {
        get => _localizer ?? PassThroughLocalizer.Instance;
        set => _localizer = value;
    }

    private sealed class PassThroughLocalizer : IStringLocalizer<SharedResource>
    {
        public static readonly PassThroughLocalizer Instance = new();

        public LocalizedString this[string name] => new(name, name, resourceNotFound: true);

        public LocalizedString this[string name, params object[] arguments] =>
            new(name, string.Format(name, arguments), resourceNotFound: true);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
