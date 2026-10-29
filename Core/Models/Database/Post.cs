namespace Social.Core.Models.Database;

/// <summary>Post, wie ihn die UI anzeigt (aus FeedPostRecord gemappt).</summary>
public class Post
{
    public long Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public User User { get; set; } = new();
    public string Content { get; set; } = string.Empty;
    public List<string> ImageUrls { get; set; } = new();
    public int LikesCount { get; set; }
    public int CommentsCount { get; set; }
    public int SharesCount { get; set; }
    public bool IsLiked { get; set; }
    public bool IsBookmarked { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string Location { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
}
