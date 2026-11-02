using Social.Core.Models.Database;
using Social.Core.Models.Enums;

namespace Social.Core.Mapping;

public static class MessagingMapper
{
    public static Notification ToNotification(this NotificationRecord record) => new()
    {
        Id = record.Id,
        Actor = new User
        {
            Id = record.ActorId,
            Username = record.Username,
            DisplayName = record.DisplayName,
            Vorname = record.Vorname,
            Nachname = record.Nachname,
            ProfileImageUrl = record.ProfileImageUrl,
            IsVerified = record.IsVerified
        },
        // type in der DB: like, comment, follow, mention, message
        Type = Enum.TryParse<NotificationType>(record.Type, ignoreCase: true, out var type) ? type : NotificationType.Like,
        PostId = record.PostId,
        PostPreviewUrl = record.PostPreviewUrl,
        PostIsVideo = record.PostMediaType == "video",
        CreatedAt = record.CreatedAt.ToLocalTime(),
        IsRead = record.IsRead
    };

    public static Conversation ToConversation(this ConversationRecord record, string? currentUserId) => new()
    {
        Id = record.Id,
        Other = new User
        {
            Id = record.OtherUserId,
            Username = record.Username,
            DisplayName = record.DisplayName,
            Vorname = record.Vorname,
            Nachname = record.Nachname,
            ProfileImageUrl = record.ProfileImageUrl,
            IsVerified = record.IsVerified
        },
        LastMessage = record.LastMessage,
        LastMessageIsMine = record.LastSenderId == currentUserId,
        LastMessageAt = record.LastMessageAt.ToLocalTime(),
        UnreadCount = record.UnreadCount
    };

    public static ChatMessage ToChatMessage(this MessageRecord record, string? currentUserId) => new()
    {
        Id = record.Id,
        ConversationId = record.ConversationId,
        SenderId = record.SenderId,
        Content = record.Content,
        CreatedAt = record.CreatedAt.ToLocalTime(),
        IsMine = record.SenderId == currentUserId
    };
}
