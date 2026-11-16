using Social.Core.Mapping;
using Social.Core.Models.Database;

namespace Social.Tests;

public class MappingTests
{
    [Fact]
    public void ChatMessage_MapsStoryImageEditAndDelete()
    {
        var updated = new DateTime(2026, 11, 16, 10, 0, 0, DateTimeKind.Utc);
        var record = new MessageRecord
        {
            Id = 1,
            SenderId = TestData.MeId,
            Content = "",
            StoryUrl = "https://x/stories/a.mp4",
            StoryId = null,
            ImagePath = "1/me/a.jpg",
            EditedAt = updated,
            DeletedAt = updated,
            UpdatedAt = updated
        };

        var message = record.ToChatMessage(TestData.MeId);

        Assert.True(message.IsMine);
        Assert.True(message.IsStoryReply);
        Assert.False(message.StoryAvailable);
        Assert.True(message.StoryIsVideo);
        Assert.Equal("1/me/a.jpg", message.ImagePath);
        Assert.True(message.IsEdited);
        Assert.True(message.IsDeleted);
        Assert.Equal(updated, message.UpdatedAtUtc);
    }

    [Fact]
    public void Conversation_GroupWithoutMessages_HasNameAndAvatarFromTitle()
    {
        var record = new ConversationRecord
        {
            Id = 3,
            IsGroup = true,
            Title = "Trip 2027",
            MemberCount = 4,
            OtherUserId = null!,
            DisplayName = null!,
            LastMessage = null!,
            LastSenderId = null
        };

        var conversation = record.ToConversation(TestData.MeId);

        Assert.True(conversation.IsGroup);
        Assert.False(conversation.HasMessages);
        Assert.False(conversation.LastMessageIsMine);
        Assert.Equal("Trip 2027", conversation.Name);
        Assert.Equal("T", conversation.AvatarUser.Initials);
        Assert.Equal("", conversation.LastMessage);
    }

    [Fact]
    public void Post_MapsEditedAtToLocalTime()
    {
        var edited = new DateTime(2026, 11, 16, 10, 0, 0, DateTimeKind.Utc);
        var post = new FeedPostRecord { Id = 1, UserId = "u", EditedAt = edited }.ToPost();

        Assert.Equal(edited.ToLocalTime(), post.EditedAt);
    }

    [Fact]
    public void Story_MapsMediaType()
    {
        var story = new StoryFeedRecord { Id = 1, MediaType = "video" }.ToStory();

        Assert.True(story.IsVideo);
    }
}
