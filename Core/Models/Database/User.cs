using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Social.core.Models;

[Table("users")]
public class User : BaseModel
{   
    [PrimaryKey("id")]
    public string Id { get; set; }
    
    [Column("username")]
    public string Username { get; set; } = string.Empty;
    
    [Column("display_name")]
    public string DisplayName { get; set; } = string.Empty;
    
    [Column("bio")]
    public string Bio { get; set; } = string.Empty;
    
    [Column("profile_image_url")]
    public string ProfileImageUrl { get; set; } = string.Empty;
    
    [Column("followers_count")]
    public int FollowersCount { get; set; }
    
    [Column("following_count")]
    public int FollowingCount { get; set; }
    
    [Column("posts_count")]
    public int PostsCount { get; set; }
    
    [Column("is_verified")]
    public bool IsVerified { get; set; }
    
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}