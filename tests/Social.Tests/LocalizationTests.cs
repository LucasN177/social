using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Social.Resources;

namespace Social.Tests;

/// <summary>
/// Prüft die deutschen Texte: Ressourcen werden geladen, und jeder Text im Code, der übersetzt
/// angezeigt wird, hat einen Eintrag in Resources/SharedResource.de.resx (Schlüssel = englischer Text).
/// </summary>
public class LocalizationTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    // L["…"] / Loc.L["…"] mit festem Text
    private static readonly Regex LocalizerKey = new(@"\bL\[""((?:[^""\\]|\\.)+)""", RegexOptions.Compiled);

    // Feste Meldungen, die FeedbackService bzw. die Anzeige (@L[_errorMessage]) übersetzt
    private static readonly Regex MessageLiteral = new(
        @"(?:Feedback\.(?:Success|Info|Warning|Error)|Response(?:<[^>]+>)?\.Failure|Feedback\.Handle\([^,]+,|Feedback\.Error\([^,]+,)\(?\s*""((?:[^""\\]|\\.)+)""",
        RegexOptions.Compiled);

    private static readonly Lazy<Dictionary<string, string>> German = new(() =>
        XDocument.Load(Path.Combine(RepoRoot, "Resources", "SharedResource.de.resx"))
            .Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string)d.Element("value")!));

    [Fact]
    public void GermanResourcesAreLoaded_ForGermanCulture()
    {
        var services = new ServiceCollection().AddLogging().AddLocalization().BuildServiceProvider();
        var localizer = services.GetRequiredService<IStringLocalizer<SharedResource>>();

        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-DE");
            Assert.Equal("Speichern", localizer["Save"].Value);
            Assert.Equal("Antwort an bob gesendet", localizer["Reply sent to {0}", "bob"].Value.Replace("„", "").Replace("“", ""));

            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            Assert.Equal("Save", localizer["Save"].Value);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void EveryLocalizerKeyInTheUi_HasAGermanTranslation()
    {
        var missing = SourceFiles("Pages", "Layout", "Services")
            .SelectMany(f => LocalizerKey.Matches(File.ReadAllText(f)).Select(m => (File: Path.GetFileName(f), Key: Unescape(m.Groups[1].Value))))
            .Where(x => !German.Value.ContainsKey(x.Key))
            .Select(x => $"{x.File}: {x.Key}")
            .Distinct()
            .ToList();

        Assert.True(missing.Count == 0, "Fehlende Übersetzungen:\n" + string.Join("\n", missing));
    }

    [Fact]
    public void EveryFixedMessage_HasAGermanTranslation()
    {
        var missing = SourceFiles("Pages", "Layout", "Services")
            .SelectMany(f => MessageLiteral.Matches(File.ReadAllText(f)).Select(m => (File: Path.GetFileName(f), Key: Unescape(m.Groups[1].Value))))
            // Interpolierte Texte ($"…{x}…") lassen sich nicht als Schlüssel prüfen
            .Where(x => !x.Key.Contains('{'))
            .Where(x => !German.Value.ContainsKey(x.Key))
            .Select(x => $"{x.File}: {x.Key}")
            .Distinct()
            .ToList();

        Assert.True(missing.Count == 0, "Fehlende Übersetzungen für Meldungen:\n" + string.Join("\n", missing));
    }

    [Fact]
    public void Translations_KeepAllPlaceholders()
    {
        static string Placeholders(string s) =>
            string.Join(",", Regex.Matches(s, @"\{(\d+)\}").Select(m => m.Value).Order());

        var broken = German.Value
            .Where(e => Placeholders(e.Key) != Placeholders(e.Value))
            .Select(e => $"{e.Key} → {e.Value}")
            .ToList();

        Assert.True(broken.Count == 0, "Platzhalter stimmen nicht:\n" + string.Join("\n", broken));
    }

    private static IEnumerable<string> SourceFiles(params string[] folders) =>
        folders.SelectMany(folder => Directory.EnumerateFiles(Path.Combine(RepoRoot, folder), "*.*", SearchOption.AllDirectories))
            .Where(f => f.EndsWith(".razor") || f.EndsWith(".cs"));

    private static string Unescape(string s) => s.Replace("\\\"", "\"").Replace("\\\\", "\\");

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Social.csproj")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Social.csproj nicht gefunden");
    }
}
