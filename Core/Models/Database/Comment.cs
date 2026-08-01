namespace Social.core.Models;

public class Comment
{   
    public int Id { get; set; }
    public User User { get; set; } = new();
    public string Content { get; set; } = string.Empty;
    public int LikesCount { get; set; }
    public bool IsLiked { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}