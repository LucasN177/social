using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Social.Core.Models.Database;

// Supabase-Modelle für Phase 5 (016_notifications_messages.sql). Mapping in Core/Mapping/MessagingMapper.cs.

/// <summary>Zeile der View notification_feed – nur lesend.</summary>
[Table("notification_feed")]
public class NotificationRecord : BaseModel
{
    [PrimaryKey("id")] public long Id { get; set; }
    [Column("user_id")] public string UserId { get; set; } = string.Empty;
    [Column("actor_id")] public string ActorId { get; set; } = string.Empty;
    [Column("type")] public string Type { get; set; } = string.Empty;
    [Column("post_id")] public long? PostId { get; set; }
    [Column("is_read")] public bool IsRead { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("username")] public string? Username { get; set; }
    [Column("display_name")] public string DisplayName { get; set; } = string.Empty;
    [Column("vorname")] public string Vorname { get; set; } = string.Empty;
    [Column("nachname")] public string Nachname { get; set; } = string.Empty;
    [Column("profile_image_url")] public string ProfileImageUrl { get; set; } = string.Empty;
    [Column("is_verified")] public bool IsVerified { get; set; }
    [Column("post_preview_url")] public string? PostPreviewUrl { get; set; }
    [Column("post_media_type")] public string? PostMediaType { get; set; }
}

/// <summary>Zeile der View conversation_list – nur lesend.</summary>
[Table("conversation_list")]
public class ConversationRecord : BaseModel
{
    [PrimaryKey("id")] public long Id { get; set; }
    [Column("last_message_at")] public DateTime LastMessageAt { get; set; }
    [Column("other_user_id")] public string OtherUserId { get; set; } = string.Empty;
    [Column("username")] public string? Username { get; set; }
    [Column("display_name")] public string DisplayName { get; set; } = string.Empty;
    [Column("vorname")] public string Vorname { get; set; } = string.Empty;
    [Column("nachname")] public string Nachname { get; set; } = string.Empty;
    [Column("profile_image_url")] public string ProfileImageUrl { get; set; } = string.Empty;
    [Column("is_verified")] public bool IsVerified { get; set; }
    [Column("last_message")] public string LastMessage { get; set; } = string.Empty;
    [Column("last_sender_id")] public string LastSenderId { get; set; } = string.Empty;
    [Column("unread_count")] public int UnreadCount { get; set; }
}

[Table("conversation_members")]
public class ConversationMemberRecord : BaseModel
{
    [PrimaryKey("conversation_id", true)] public long ConversationId { get; set; }
    [PrimaryKey("user_id", true)] public string UserId { get; set; } = string.Empty;
}

[Table("messages")]
public class MessageRecord : BaseModel
{
    [PrimaryKey("id", false)] public long Id { get; set; }
    [Column("conversation_id")] public long ConversationId { get; set; }
    [Column("sender_id")] public string SenderId { get; set; } = string.Empty;
    [Column("content")] public string Content { get; set; } = string.Empty;
    [Column("created_at", ignoreOnInsert: true, ignoreOnUpdate: true)] public DateTime CreatedAt { get; set; }
}
