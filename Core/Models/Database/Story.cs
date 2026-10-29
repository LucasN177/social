namespace Social.Core.Models.Database;

/// <summary>Story, wie sie die UI anzeigt (aus StoryFeedRecord gemappt).</summary>
public class Story
{
    public long Id { get; set; }
    public User User { get; set; } = new();
    public string ImageUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime ExpiresAt { get; set; }
    public bool IsViewed { get; set; }
    /// <summary>Nur für eigene Stories aussagekräftig (RLS auf story_views).</summary>
    public int ViewCount { get; set; }
}

/// <summary>Alle aktiven Stories eines Users, chronologisch.</summary>
public record StoryGroup(User User, List<Story> Stories)
{
    public bool AllViewed => Stories.All(s => s.IsViewed);
}
