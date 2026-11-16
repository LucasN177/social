using Social.Core.Interfaces.Response;

namespace Social.Core.Interfaces.Services;

/// <summary>
/// Zentrale Rückmeldungen an den Nutzer (Snackbar). Services liefern <see cref="IResponse"/>,
/// die UI gibt Ergebnisse nur noch hierüber aus – technische Fehlertexte (Exception-Meldungen)
/// erreichen den Nutzer dabei nie, sie werden protokolliert und durch verständliche Texte ersetzt.
/// </summary>
public interface IFeedbackService
{
    const string DefaultError = "That didn't work. Please try again.";
    const string OfflineError = "You seem to be offline. Please check your connection.";

    void Success(string message);
    void Info(string message);
    void Warning(string message);
    void Error(string message);

    /// <summary>Zeigt die Fehlermeldung des Ergebnisses – oder <paramref name="fallback"/>, wenn sie fehlt oder technisch ist.</summary>
    void Error(IResponse result, string fallback = DefaultError);

    /// <summary>Erfolg → optional <paramref name="successMessage"/>, Fehler → <see cref="Error(IResponse, string)"/>. Liefert IsSuccess.</summary>
    bool Handle(IResponse result, string? successMessage = null, string fallback = DefaultError);

    /// <summary>Für unerwartete Ausnahmen (z. B. aus dem ErrorBoundary): protokollieren und allgemeine Meldung zeigen.</summary>
    void Unexpected(Exception exception, string? context = null);

    /// <summary>Text, der dem Nutzer für dieses Ergebnis angezeigt würde (null bei Erfolg).</summary>
    string? UserMessage(IResponse result, string fallback = DefaultError);
}
