using Social.Core.Interfaces.Response;
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

    const int MaxGroupMembers = 32;
    const int MaxGroupTitleLength = 60;
    const int MaxMessageLength = 2000;

    /// <summary>Kopf des Chats inkl. Mitglieder; null, wenn man kein Mitglied ist.</summary>
    Task<ChatInfo?> GetChatAsync(long conversationId);
    Task<List<ChatMessage>> GetLatestMessagesAsync(long conversationId);
    /// <summary>Neue und bearbeitete/gelöschte Nachrichten seit <paramref name="sinceUtc"/> (Polling).</summary>
    Task<List<ChatMessage>> GetChangesSinceAsync(long conversationId, DateTime sinceUtc);

    /// <summary>Text und/oder Bild (bereits verkleinertes JPEG) senden.</summary>
    Task<IResponse<ChatMessage>> SendAsync(long conversationId, string content, byte[]? image = null);
    Task<IResponse> EditAsync(ChatMessage message, string content);
    /// <summary>Löscht die eigene Nachricht (Platzhalter bleibt) inkl. Bild.</summary>
    Task<IResponse> DeleteAsync(ChatMessage message);

    Task<IResponse<long>> CreateGroupAsync(string title, IReadOnlyCollection<User> members);
    Task<IResponse> AddMembersAsync(long conversationId, IReadOnlyCollection<User> members);
    Task<IResponse> RenameGroupAsync(long conversationId, string title);
    Task<IResponse> LeaveGroupAsync(long conversationId);

    Task MarkReadAsync(long conversationId);
    Task<int> GetUnreadCountAsync();
}
