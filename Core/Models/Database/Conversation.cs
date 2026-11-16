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
    /// <summary>Antwort auf eine Story (story_url gesetzt).</summary>
    public bool IsStoryReply => StoryUrl is not null;
    /// <summary>URL des Story-Bilds/-Videos; nur anzeigen, solange <see cref="StoryAvailable"/> gilt.</summary>
    public string? StoryUrl { get; set; }
    /// <summary>false, sobald die Story gelöscht oder aufgeräumt wurde (story_id ist dann NULL).</summary>
    public bool StoryAvailable { get; set; }
    public bool StoryIsVideo =>
        StoryUrl is not null && (StoryUrl.EndsWith(".mp4") || StoryUrl.EndsWith(".webm") || StoryUrl.EndsWith(".mov"));
}
