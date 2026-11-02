using Social.Core.Models.Database;

namespace Social.Core.Interfaces.Services;

public interface INotificationService
{
    const int PageSize = 20;

    Task<List<Notification>> GetNotificationsAsync(int page);
    Task<int> GetUnreadCountAsync();
    Task MarkAllReadAsync();
}

public interface IMessageService
{
    Task<List<Conversation>> GetConversationsAsync();

    /// <summary>Findet oder erstellt die 1:1-Unterhaltung mit <paramref name="other"/>; null bei Fehler.</summary>
    Task<long?> OpenConversationAsync(User other);

    Task<User?> GetPartnerAsync(long conversationId);
    Task<List<ChatMessage>> GetLatestMessagesAsync(long conversationId);
    Task<List<ChatMessage>> GetMessagesAfterAsync(long conversationId, long afterId);
    Task<ChatMessage?> SendAsync(long conversationId, string content);
    Task MarkReadAsync(long conversationId);
    Task<int> GetUnreadCountAsync();
}
