namespace Social.core.Models;

public class Story
{
    public int Id { get; set; }
    public User User { get; set; } = new();
    public string ImageUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public bool IsViewed { get; set; }    
}