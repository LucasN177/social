using Social.Core.Interfaces.Services;

namespace Social.Services;

/// <summary>
/// Hält die Ungelesen-Zähler (Benachrichtigungen, Nachrichten) für die Badges aktuell.
/// Supabase-Realtime läuft mit dem C#-Paket nicht im Browser (siehe Implementationsplan) – daher Polling.
/// </summary>
public class ActivityService(INotificationService notificationService, IMessageService messageService) : IAsyncDisposable
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);
    private CancellationTokenSource? _cts;

    public int UnreadNotifications { get; private set; }
    public int UnreadMessages { get; private set; }
    public event Action? Changed;

    public void Start()
    {
        if (_cts is not null) return;
        _cts = new CancellationTokenSource();
        _ = PollAsync(_cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts = null;
        UnreadNotifications = UnreadMessages = 0;
        Changed?.Invoke();
    }

    public async Task RefreshAsync()
    {
        var notifications = notificationService.GetUnreadCountAsync();
        var messages = messageService.GetUnreadCountAsync();
        await Task.WhenAll(notifications, messages);

        if (UnreadNotifications == notifications.Result && UnreadMessages == messages.Result) return;
        UnreadNotifications = notifications.Result;
        UnreadMessages = messages.Result;
        Changed?.Invoke();
    }

    private async Task PollAsync(CancellationToken token)
    {
        try
        {
            await RefreshAsync();
            using var timer = new PeriodicTimer(Interval);
            while (await timer.WaitForNextTickAsync(token))
                await RefreshAsync();
        }
        catch (OperationCanceledException)
        {
            // gestoppt (Logout)
        }
    }

    public ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        return ValueTask.CompletedTask;
    }
}
