using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Social.Core.Models.Database;

// Supabase-Modelle für Phase 2. Die UI arbeitet mit Post / Comment, das Mapping liegt in Core/Mapping/PostMapper.cs.

/// <summary>Zeile der View feed_posts (009 / 013) – nur lesend.</summary>
[Table("feed_posts")]
public class FeedPostRecord : BaseModel
{
    [PrimaryKey("id")] public long Id { get; set; }
    [Column("user_id")] public string UserId { get; set; } = string.Empty;
    [Column("content")] public string Content { get; set; } = string.Empty;
    [Column("location")] public string Location { get; set; } = string.Empty;
    [Column("tags")] public List<string> Tags { get; set; } = new();
    [Column("media_type")] public string MediaType { get; set; } = "image";
    [Column("likes_count")] public int LikesCount { get; set; }
    [Column("comments_count")] public int CommentsCount { get; set; }
    [Column("shares_count")] public int SharesCount { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("username")] public string? Username { get; set; }
    [Column("display_name")] public string DisplayName { get; set; } = string.Empty;
    [Column("vorname")] public string Vorname { get; set; } = string.Empty;
    [Column("nachname")] public string Nachname { get; set; } = string.Empty;
    [Column("profile_image_url")] public string ProfileImageUrl { get; set; } = string.Empty;
    [Column("is_verified")] public bool IsVerified { get; set; }
    /// <summary>Bild-URLs bzw. bei Reels die Video-URL.</summary>
    [Column("image_urls")] public List<string> ImageUrls { get; set; } = new();
    [Column("is_liked")] public bool IsLiked { get; set; }
    [Column("is_bookmarked")] public bool IsBookmarked { get; set; }
}

/// <summary>Zeile der View home_feed (014): eigene Posts + Posts gefolgter Accounts.</summary>
[Table("home_feed")]
public class HomeFeedRecord : FeedPostRecord;

/// <summary>Ergebnis der RPC search_hashtags (014).</summary>
public class HashtagResult
{
    [Newtonsoft.Json.JsonProperty("tag")] public string Tag { get; set; } = string.Empty;
    [Newtonsoft.Json.JsonProperty("post_count")] public int PostCount { get; set; }
}

[Table("follows")]
public class FollowRecord : BaseModel
{
    [PrimaryKey("follower_id", true)] public string FollowerId { get; set; } = string.Empty;
    [PrimaryKey("following_id", true)] public string FollowingId { get; set; } = string.Empty;
}

/// <summary>Zeile der Tabelle posts – zum Löschen.</summary>
[Table("posts")]
public class PostRecord : BaseModel
{
    [PrimaryKey("id")] public long Id { get; set; }
    [Column("user_id")] public string UserId { get; set; } = string.Empty;
}

/// <summary>Zeile der View comment_details (013) – nur lesend.</summary>
[Table("comment_details")]
public class CommentDetailRecord : BaseModel
{
    [PrimaryKey("id")] public long Id { get; set; }
    [Column("post_id")] public long PostId { get; set; }
    [Column("user_id")] public string UserId { get; set; } = string.Empty;
    [Column("content")] public string Content { get; set; } = string.Empty;
    [Column("likes_count")] public int LikesCount { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("username")] public string? Username { get; set; }
    [Column("display_name")] public string DisplayName { get; set; } = string.Empty;
    [Column("vorname")] public string Vorname { get; set; } = string.Empty;
    [Column("nachname")] public string Nachname { get; set; } = string.Empty;
    [Column("profile_image_url")] public string ProfileImageUrl { get; set; } = string.Empty;
    [Column("is_verified")] public bool IsVerified { get; set; }
}

/// <summary>Zeile der Tabelle comments – zum Anlegen und Löschen.</summary>
[Table("comments")]
public class CommentRecord : BaseModel
{
    [PrimaryKey("id", false)] public long Id { get; set; }
    [Column("post_id")] public long PostId { get; set; }
    [Column("user_id")] public string UserId { get; set; } = string.Empty;
    [Column("content")] public string Content { get; set; } = string.Empty;
    [Column("created_at", ignoreOnInsert: true, ignoreOnUpdate: true)] public DateTime CreatedAt { get; set; }
}

/// <summary>Zeile der View story_feed (015) – nur lesend.</summary>
[Table("story_feed")]
public class StoryFeedRecord : BaseModel
{
    [PrimaryKey("id")] public long Id { get; set; }
    [Column("user_id")] public string UserId { get; set; } = string.Empty;
    [Column("image_url")] public string ImageUrl { get; set; } = string.Empty;
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("expires_at")] public DateTime ExpiresAt { get; set; }
    [Column("username")] public string? Username { get; set; }
    [Column("display_name")] public string DisplayName { get; set; } = string.Empty;
    [Column("vorname")] public string Vorname { get; set; } = string.Empty;
    [Column("nachname")] public string Nachname { get; set; } = string.Empty;
    [Column("profile_image_url")] public string ProfileImageUrl { get; set; } = string.Empty;
    [Column("is_verified")] public bool IsVerified { get; set; }
    [Column("is_viewed")] public bool IsViewed { get; set; }
    [Column("view_count")] public int ViewCount { get; set; }
}

/// <summary>Zeile der Tabelle stories – zum Anlegen und Löschen.</summary>
[Table("stories")]
public class StoryRecord : BaseModel
{
    [PrimaryKey("id", false)] public long Id { get; set; }
    [Column("user_id")] public string UserId { get; set; } = string.Empty;
    [Column("image_url")] public string ImageUrl { get; set; } = string.Empty;
}

[Table("likes")]
public class LikeRecord : BaseModel
{
    [PrimaryKey("post_id", true)] public long PostId { get; set; }
    [PrimaryKey("user_id", true)] public string UserId { get; set; } = string.Empty;
}

[Table("bookmarks")]
public class BookmarkRecord : BaseModel
{
    [PrimaryKey("post_id", true)] public long PostId { get; set; }
    [PrimaryKey("user_id", true)] public string UserId { get; set; } = string.Empty;
}
