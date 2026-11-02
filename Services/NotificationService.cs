using Social.Core.Interfaces.Infrastructure;
using Social.Core.Interfaces.Services;
using Social.Core.Mapping;
using Social.Core.Models.Database;

namespace Social.Services;

public class NotificationService(INotificationRepository notificationRepository) : INotificationService
{
    public async Task<List<Notification>> GetNotificationsAsync(int page)
    {
        var result = await notificationRepository.GetNotifications(page * INotificationService.PageSize, INotificationService.PageSize);
        return result is { IsSuccess: true, Data: not null }
            ? result.Data.Select(r => r.ToNotification()).ToList()
            : new List<Notification>();
    }

    public async Task<int> GetUnreadCountAsync()
    {
        var result = await notificationRepository.GetUnreadCount();
        return result.IsSuccess ? result.Data : 0;
    }

    public Task MarkAllReadAsync() => notificationRepository.MarkAllRead();
}

public class MessageService(IMessageRepository messageRepository, IUserRepository userRepository, IAuthService authService) : IMessageService
{
    private const int HistorySize = 50;

    private string? CurrentUserId => authService.CurrentUser?.Id;

    public async Task<List<Conversation>> GetConversationsAsync()
    {
        var result = await messageRepository.GetConversations();
        return result is { IsSuccess: true, Data: not null }
            ? result.Data.Select(r => r.ToConversation(CurrentUserId)).ToList()
            : new List<Conversation>();
    }

    public async Task<long?> OpenConversationAsync(User other)
    {
        var result = await messageRepository.GetOrCreateConversation(other.Id);
        return result.IsSuccess ? result.Data : null;
    }

    public async Task<User?> GetPartnerAsync(long conversationId)
    {
        if (CurrentUserId is not { } me) return null;
        var partner = await messageRepository.GetPartnerId(conversationId, me);
        if (partner is not { IsSuccess: true, Data: not null }) return null;
        var profile = await userRepository.GetProfile(partner.Data);
        return profile.IsSuccess ? profile.Data : null;
    }

    public async Task<List<ChatMessage>> GetLatestMessagesAsync(long conversationId)
    {
        var result = await messageRepository.GetLatestMessages(conversationId, HistorySize);
        return ToMessages(result.IsSuccess ? result.Data : null);
    }

    public async Task<List<ChatMessage>> GetMessagesAfterAsync(long conversationId, long afterId)
    {
        var result = await messageRepository.GetMessagesAfter(conversationId, afterId);
        return ToMessages(result.IsSuccess ? result.Data : null);
    }

    public async Task<ChatMessage?> SendAsync(long conversationId, string content)
    {
        content = content.Trim();
        if (CurrentUserId is not { } me || content.Length is 0 or > 2000) return null;
        var result = await messageRepository.SendMessage(conversationId, me, content);
        if (result is not { IsSuccess: true, Data: not null }) return null;

        var message = result.Data.ToChatMessage(me);
        // Postgrest befüllt Spalten mit ignoreOnInsert im Insert-Ergebnis nicht – dann gilt "jetzt"
        if (result.Data.CreatedAt == default) message.CreatedAt = DateTime.Now;
        return message;
    }

    public Task MarkReadAsync(long conversationId) => messageRepository.MarkRead(conversationId);

    public async Task<int> GetUnreadCountAsync()
    {
        var result = await messageRepository.GetUnreadCount();
        return result.IsSuccess ? result.Data : 0;
    }

    private List<ChatMessage> ToMessages(List<MessageRecord>? records) =>
        records?.Select(r => r.ToChatMessage(CurrentUserId)).ToList() ?? new List<ChatMessage>();
}
