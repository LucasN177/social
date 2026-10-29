namespace Social.Core.Models.Database;

/// <summary>Kommentar, wie ihn die UI anzeigt (aus CommentDetailRecord gemappt).</summary>
public class Comment
{
    public long Id { get; set; }
    public long PostId { get; set; }
    public User User { get; set; } = new();
    public string Content { get; set; } = string.Empty;
    public int LikesCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
