using Social.Core.Interfaces.Response;
using Social.Core.Models.Database;

namespace Social.Core.Interfaces.Infrastructure;

public interface INotificationRepository
{
    public Task<IResponse<List<NotificationRecord>>> GetNotifications(int offset, int limit);

    public Task<IResponse<int>> GetUnreadCount();

    public Task<IResponse> MarkAllRead();
}

public interface IMessageRepository
{
    public Task<IResponse<List<ConversationRecord>>> GetConversations();

    public Task<IResponse<long>> GetOrCreateConversation(string otherUserId);

    /// <summary>ID des Gegenübers einer 1:1-Unterhaltung (auch wenn sie noch leer ist).</summary>
    public Task<IResponse<string>> GetPartnerId(long conversationId, string currentUserId);

    /// <summary>Die neuesten <paramref name="limit"/> Nachrichten, älteste zuerst.</summary>
    public Task<IResponse<List<MessageRecord>>> GetLatestMessages(long conversationId, int limit);

    /// <summary>Nachrichten mit ID größer <paramref name="afterId"/> (für Polling), älteste zuerst.</summary>
    public Task<IResponse<List<MessageRecord>>> GetMessagesAfter(long conversationId, long afterId);

    public Task<IResponse<MessageRecord>> SendMessage(long conversationId, string senderId, string content);

    public Task<IResponse> MarkRead(long conversationId);

    public Task<IResponse<int>> GetUnreadCount();
}
