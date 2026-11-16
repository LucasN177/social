using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Social.Core.Interfaces.Services;

namespace Social.Layout;

/// <summary>
/// Fängt unerwartete Fehler einer Seite ab: protokollieren, Snackbar zeigen und statt der
/// Blazor-Fehlerleiste nur den betroffenen Bereich durch <c>ErrorContent</c> ersetzen.
/// </summary>
public class AppErrorBoundary : ErrorBoundary
{
    [Inject] private IFeedbackService Feedback { get; set; } = null!;

    protected override Task OnErrorAsync(Exception exception)
    {
        Feedback.Unexpected(exception, "page");
        return Task.CompletedTask;
    }
}
