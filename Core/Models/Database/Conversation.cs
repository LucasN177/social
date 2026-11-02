namespace Social.Core.Models.Database;

/// <summary>Eintrag der Chatliste (aus ConversationRecord gemappt).</summary>
public class Conversation
{
    public long Id { get; set; }
    public User Other { get; set; } = new();
    public string LastMessage { get; set; } = string.Empty;
    public bool LastMessageIsMine { get; set; }
    public DateTime LastMessageAt { get; set; }
    public int UnreadCount { get; set; }
}

/// <summary>Einzelne Nachricht im Chat.</summary>
public class ChatMessage
{
    public long Id { get; set; }
    public long ConversationId { get; set; }
    public string SenderId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsMine { get; set; }
}
