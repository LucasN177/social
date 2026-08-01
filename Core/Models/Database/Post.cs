namespace Social.core.Models;

public class Post
{
    public int Id { get; set; }
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