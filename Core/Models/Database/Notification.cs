using Social.Core.Models.Enums;

namespace Social.Core.Models.Database;

/// <summary>Benachrichtigung, wie sie die UI anzeigt (aus NotificationRecord gemappt).</summary>
public class Notification
{
    public long Id { get; set; }
    /// <summary>Wer die Benachrichtigung ausgelöst hat.</summary>
    public User Actor { get; set; } = new();
    public NotificationType Type { get; set; }
    public long? PostId { get; set; }
    public string? PostPreviewUrl { get; set; }
    public bool PostIsVideo { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public bool IsRead { get; set; }
}
