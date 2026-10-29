using Social.Core.Models.Database;

namespace Social.Core.Mapping;

public static class PostMapper
{
    public static Post ToPost(this FeedPostRecord record) => new()
    {
        Id = record.Id,
        UserId = record.UserId,
        User = new User
        {
            Id = record.UserId,
            Username = record.Username,
            DisplayName = record.DisplayName,
            Vorname = record.Vorname,
            Nachname = record.Nachname,
            ProfileImageUrl = record.ProfileImageUrl,
            IsVerified = record.IsVerified
        },
        Content = record.Content,
        Location = record.Location,
        Tags = record.Tags,
        MediaType = record.MediaType,
        ImageUrls = record.ImageUrls,
        LikesCount = record.LikesCount,
        CommentsCount = record.CommentsCount,
        SharesCount = record.SharesCount,
        IsLiked = record.IsLiked,
        IsBookmarked = record.IsBookmarked,
        CreatedAt = record.CreatedAt.ToLocalTime()
    };

    public static Story ToStory(this StoryFeedRecord record) => new()
    {
        Id = record.Id,
        User = new User
        {
            Id = record.UserId,
            Username = record.Username,
            DisplayName = record.DisplayName,
            Vorname = record.Vorname,
            Nachname = record.Nachname,
            ProfileImageUrl = record.ProfileImageUrl,
            IsVerified = record.IsVerified
        },
        ImageUrl = record.ImageUrl,
        CreatedAt = record.CreatedAt.ToLocalTime(),
        ExpiresAt = record.ExpiresAt.ToLocalTime(),
        IsViewed = record.IsViewed,
        ViewCount = record.ViewCount
    };

    public static Comment ToComment(this CommentDetailRecord record) => new()
    {
        Id = record.Id,
        PostId = record.PostId,
        User = new User
        {
            Id = record.UserId,
            Username = record.Username,
            DisplayName = record.DisplayName,
            Vorname = record.Vorname,
            Nachname = record.Nachname,
            ProfileImageUrl = record.ProfileImageUrl,
            IsVerified = record.IsVerified
        },
        Content = record.Content,
        LikesCount = record.LikesCount,
        CreatedAt = record.CreatedAt.ToLocalTime()
    };
}
