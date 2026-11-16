using Microsoft.Extensions.Logging;
using MudBlazor;
using Social.Core.Interfaces.Response;
using Social.Core.Interfaces.Services;

namespace Social.Services;

public class FeedbackService(ISnackbar snackbar, ILogger<FeedbackService> logger) : IFeedbackService
{
    public void Success(string message) => snackbar.Add(message, Severity.Success);

    public void Info(string message) => snackbar.Add(message, Severity.Normal);

    public void Warning(string message) => snackbar.Add(message, Severity.Warning);

    public void Error(string message) => snackbar.Add(message, Severity.Error);

    public void Error(IResponse result, string fallback = IFeedbackService.DefaultError) =>
        Error(UserMessage(result, fallback) ?? fallback);

    public bool Handle(IResponse result, string? successMessage = null, string fallback = IFeedbackService.DefaultError)
    {
        if (result.IsSuccess)
        {
            if (successMessage is not null) Success(successMessage);
            return true;
        }

        Error(result, fallback);
        return false;
    }

    public void Unexpected(Exception exception, string? context = null)
    {
        logger.LogError(exception, "Unexpected error{Context}", context is null ? "" : $" in {context}");
        Error(IsOffline(exception) ? IFeedbackService.OfflineError : "Something went wrong. Please try again.");
    }

    public string? UserMessage(IResponse result, string fallback = IFeedbackService.DefaultError)
    {
        if (result.IsSuccess) return null;

        if (result.Exception is { } exception)
        {
            logger.LogWarning(exception, "Operation failed: {Message}", result.ErrorMessage ?? exception.Message);
            if (IsOffline(exception)) return IFeedbackService.OfflineError;
        }

        return IsTechnical(result) ? fallback : result.ErrorMessage!;
    }

    /// <summary>
    /// Repositories geben bei Ausnahmen ex.Message als ErrorMessage weiter (z. B. Postgrest-JSON).
    /// Solche Texte – und leere – gelten als technisch; Services formulieren ihre Meldungen selbst.
    /// </summary>
    internal static bool IsTechnical(IResponse result) =>
        string.IsNullOrWhiteSpace(result.ErrorMessage)
        || (result.Exception is not null && result.ErrorMessage == result.Exception.Message)
        || result.ErrorMessage.TrimStart().StartsWith('{');

    private static bool IsOffline(Exception exception) =>
        exception is HttpRequestException || exception.InnerException is HttpRequestException;
}
