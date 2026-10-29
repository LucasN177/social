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
        ImageUrls = record.ImageUrls,
        LikesCount = record.LikesCount,
        CommentsCount = record.CommentsCount,
        SharesCount = record.SharesCount,
        IsLiked = record.IsLiked,
        IsBookmarked = record.IsBookmarked,
        CreatedAt = record.CreatedAt.ToLocalTime()
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
