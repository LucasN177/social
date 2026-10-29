using Social.Core.Models.Enums;

namespace Social.Core.Models.Database;

public class Notification
{
    public int Id { get; set; }
    public User User { get; set; } = new();
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public bool IsRead { get; set; }    
}