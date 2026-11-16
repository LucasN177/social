namespace Social.Core.Models.Database;

/// <summary>Eintrag der Chatliste (aus ConversationRecord gemappt).</summary>
public class Conversation
{
    public long Id { get; set; }
    /// <summary>Bei 1:1-Chats das Gegenüber, bei Gruppen ein beliebiges anderes Mitglied (oder leer).</summary>
    public User Other { get; set; } = new();
    public bool IsGroup { get; set; }
    public string? Title { get; set; }
    public int MemberCount { get; set; }
    public string LastMessage { get; set; } = string.Empty;
    public bool LastMessageIsMine { get; set; }
    public bool LastMessageIsImage { get; set; }
    public bool LastMessageIsDeleted { get; set; }
    /// <summary>Vorname (bzw. Anzeigename) des letzten Absenders – für die Vorschau in Gruppen.</summary>
    public string? LastSenderName { get; set; }
    public bool HasMessages { get; set; }
    public DateTime LastMessageAt { get; set; }
    public int UnreadCount { get; set; }

    public string Name => IsGroup
        ? Title ?? "Group"
        : string.IsNullOrWhiteSpace(Other.DisplayName) ? Other.Username ?? "Unknown" : Other.DisplayName;

    /// <summary>Für den Avatar einer Gruppe: Initiale aus dem Gruppennamen.</summary>
    public User AvatarUser => IsGroup ? new User { Vorname = Title ?? "G" } : Other;
}

/// <summary>Kopf eines Chats: 1:1 oder Gruppe inkl. Mitglieder.</summary>
public class ChatInfo
{
    public long Id { get; set; }
    public bool IsGroup { get; set; }
    public string? Title { get; set; }
    public List<User> Members { get; set; } = new();
    /// <summary>Gegenüber im 1:1-Chat.</summary>
    public User? Partner { get; set; }

    public string Name => IsGroup
        ? Title ?? "Group"
        : Partner is null ? "Chat" : string.IsNullOrWhiteSpace(Partner.DisplayName) ? Partner.Username ?? "Chat" : Partner.DisplayName;

    public User AvatarUser => IsGroup ? new User { Vorname = Title ?? "G" } : Partner ?? new User();
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

    /// <summary>Pfad im privaten Bucket "chat-images".</summary>
    public string? ImagePath { get; set; }
    /// <summary>Signierte, zeitlich begrenzte URL zum Anzeigen.</summary>
    public string? ImageUrl { get; set; }
    public DateTime? EditedAt { get; set; }
    public bool IsEdited => EditedAt is not null;
    public bool IsDeleted { get; set; }
    /// <summary>Server-Zeitpunkt der letzten Änderung (UTC) – Cursor fürs Polling.</summary>
    public DateTime UpdatedAtUtc { get; set; }
}
