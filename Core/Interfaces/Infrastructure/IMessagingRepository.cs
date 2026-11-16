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

    /// <summary>Kopf der Unterhaltung (RLS: nur für Mitglieder sichtbar).</summary>
    public Task<IResponse<ConversationInfoRecord>> GetConversation(long conversationId);

    /// <summary>Profile aller Mitglieder (RPC conversation_member_profiles, 020).</summary>
    public Task<IResponse<List<User>>> GetMembers(long conversationId);

    /// <summary>Die neuesten <paramref name="limit"/> Nachrichten, älteste zuerst.</summary>
    public Task<IResponse<List<MessageRecord>>> GetLatestMessages(long conversationId, int limit);

    /// <summary>Neue und geänderte Nachrichten mit updated_at ≥ <paramref name="sinceUtc"/> (Polling).</summary>
    public Task<IResponse<List<MessageRecord>>> GetChangesSince(long conversationId, DateTime sinceUtc);

    public Task<IResponse<MessageRecord>> SendMessage(long conversationId, string senderId, string content, string? imagePath);

    public Task<IResponse> EditMessage(long messageId, string content);

    /// <summary>Löscht weich (RPC delete_message) und liefert den alten Bildpfad (oder null).</summary>
    public Task<IResponse<string?>> DeleteMessage(long messageId);

    /// <summary>Lädt ein Bild in den privaten Bucket "chat-images" und liefert den Pfad.</summary>
    public Task<IResponse<string>> UploadChatImage(long conversationId, string userId, byte[] data, string contentType);

    public Task<IResponse> DeleteChatImages(IEnumerable<string> paths);

    /// <summary>Signierte URLs (Pfad → URL) für Bilder im privaten Bucket.</summary>
    public Task<IResponse<Dictionary<string, string>>> SignChatImages(IReadOnlyCollection<string> paths, int expiresInSeconds);

    public Task<IResponse<long>> CreateGroup(string title, IEnumerable<string> memberIds);

    public Task<IResponse> AddMembers(long conversationId, IEnumerable<string> memberIds);

    public Task<IResponse> RenameGroup(long conversationId, string title);

    public Task<IResponse> LeaveGroup(long conversationId);

    public Task<IResponse> MarkRead(long conversationId);

    public Task<IResponse<int>> GetUnreadCount();
}
