using Moq;
using Social.Core.Interfaces.Infrastructure;
using Social.Core.Models.Database;
using Social.Core.Models.Response;
using Social.Services;

namespace Social.Tests.Services;

public class MessageServiceTests
{
    private readonly Mock<IMessageRepository> _repo = new();

    private MessageService CreateService() => new(_repo.Object, TestData.AuthAs().Object);

    private static MessageRecord Record(long id, string sender = TestData.MeId, string content = "hi", string? image = null) => new()
    {
        Id = id,
        ConversationId = 1,
        SenderId = sender,
        Content = content,
        ImagePath = image,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task Send_RequiresTextOrImage()
    {
        var service = CreateService();

        var result = await service.SendAsync(1, "   ");

        Assert.False(result.IsSuccess);
        _repo.Verify(r => r.SendMessage(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task Send_WithImage_UploadsIntoConversationFolder_AndSignsUrl()
    {
        _repo.Setup(r => r.UploadChatImage(1, TestData.MeId, It.IsAny<byte[]>(), "image/jpeg"))
             .ReturnsAsync(Response<string>.Success($"1/{TestData.MeId}/a.jpg"));
        _repo.Setup(r => r.SendMessage(1, TestData.MeId, "", $"1/{TestData.MeId}/a.jpg"))
             .ReturnsAsync(Response<MessageRecord>.Success(Record(10, content: "", image: $"1/{TestData.MeId}/a.jpg")));
        _repo.Setup(r => r.SignChatImages(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<int>()))
             .ReturnsAsync(Response<Dictionary<string, string>>.Success(new() { [$"1/{TestData.MeId}/a.jpg"] = "https://signed/a" }));
        var service = CreateService();

        var result = await service.SendAsync(1, "", new byte[] { 1, 2, 3 });

        Assert.True(result.IsSuccess);
        Assert.True(result.Data!.IsMine);
        Assert.Equal("https://signed/a", result.Data.ImageUrl);
    }

    [Fact]
    public async Task Send_RemovesUploadedImage_WhenMessageFails()
    {
        _repo.Setup(r => r.UploadChatImage(1, TestData.MeId, It.IsAny<byte[]>(), "image/jpeg"))
             .ReturnsAsync(Response<string>.Success("1/me/a.jpg"));
        _repo.Setup(r => r.SendMessage(1, TestData.MeId, "caption", "1/me/a.jpg"))
             .ReturnsAsync(Response<MessageRecord>.Failure("rls"));
        var service = CreateService();

        var result = await service.SendAsync(1, "caption", new byte[] { 1 });

        Assert.False(result.IsSuccess);
        _repo.Verify(r => r.DeleteChatImages(It.Is<IEnumerable<string>>(p => p.Single() == "1/me/a.jpg")), Times.Once);
    }

    [Fact]
    public async Task SignedUrls_AreCached_BetweenPolls()
    {
        _repo.Setup(r => r.GetLatestMessages(1, It.IsAny<int>()))
             .ReturnsAsync(Response<List<MessageRecord>>.Success(new() { Record(1, image: "1/x/a.jpg") }));
        _repo.Setup(r => r.GetChangesSince(1, It.IsAny<DateTime>()))
             .ReturnsAsync(Response<List<MessageRecord>>.Success(new() { Record(1, image: "1/x/a.jpg") }));
        _repo.Setup(r => r.SignChatImages(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<int>()))
             .ReturnsAsync(Response<Dictionary<string, string>>.Success(new() { ["1/x/a.jpg"] = "https://signed/a" }));
        var service = CreateService();

        var first = await service.GetLatestMessagesAsync(1);
        var second = await service.GetChangesSinceAsync(1, DateTime.UtcNow);

        Assert.Equal("https://signed/a", first[0].ImageUrl);
        Assert.Equal("https://signed/a", second[0].ImageUrl);
        _repo.Verify(r => r.SignChatImages(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public async Task Delete_SoftDeletes_AndRemovesImage()
    {
        _repo.Setup(r => r.DeleteMessage(10)).ReturnsAsync(Response<string?>.Success("1/me/a.jpg"));
        var service = CreateService();
        var message = new ChatMessage { Id = 10, IsMine = true, Content = "x", ImagePath = "1/me/a.jpg", ImageUrl = "u" };

        var result = await service.DeleteAsync(message);

        Assert.True(result.IsSuccess);
        Assert.True(message.IsDeleted);
        Assert.Equal("", message.Content);
        Assert.Null(message.ImageUrl);
        _repo.Verify(r => r.DeleteChatImages(It.Is<IEnumerable<string>>(p => p.Single() == "1/me/a.jpg")), Times.Once);
    }

    [Fact]
    public async Task EditAndDelete_OnlyForOwnMessages()
    {
        var service = CreateService();
        var foreign = new ChatMessage { Id = 10, IsMine = false, Content = "x" };

        Assert.False((await service.EditAsync(foreign, "y")).IsSuccess);
        Assert.False((await service.DeleteAsync(foreign)).IsSuccess);
        _repo.Verify(r => r.EditMessage(It.IsAny<long>(), It.IsAny<string>()), Times.Never);
        _repo.Verify(r => r.DeleteMessage(It.IsAny<long>()), Times.Never);
    }

    [Fact]
    public async Task Edit_MarksMessageAsEdited()
    {
        _repo.Setup(r => r.EditMessage(10, "fixed")).ReturnsAsync(Response.Success());
        var service = CreateService();
        var message = new ChatMessage { Id = 10, IsMine = true, Content = "fxied" };

        var result = await service.EditAsync(message, " fixed ");

        Assert.True(result.IsSuccess);
        Assert.Equal("fixed", message.Content);
        Assert.True(message.IsEdited);
    }

    [Fact]
    public async Task CreateGroup_NeedsTwoOtherMembers_AndAName()
    {
        var service = CreateService();
        var bob = TestData.User(TestData.OtherId, "bob");

        Assert.False((await service.CreateGroupAsync("Trip", new[] { bob })).IsSuccess);
        Assert.False((await service.CreateGroupAsync("  ", new[] { bob, TestData.User("3", "carol") })).IsSuccess);
        _repo.Verify(r => r.CreateGroup(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    [Fact]
    public async Task CreateGroup_ExcludesCurrentUser_FromMemberIds()
    {
        _repo.Setup(r => r.CreateGroup("Trip", It.IsAny<IEnumerable<string>>())).ReturnsAsync(Response<long>.Success(5));
        var service = CreateService();
        var me = TestData.User(TestData.MeId, "me");

        var result = await service.CreateGroupAsync(" Trip ", new[] { me, TestData.User("2", "bob"), TestData.User("3", "carol") });

        Assert.True(result.IsSuccess);
        _repo.Verify(r => r.CreateGroup("Trip", It.Is<IEnumerable<string>>(ids => ids.SequenceEqual(new[] { "2", "3" }))), Times.Once);
    }

    [Fact]
    public async Task GetChat_DirectChat_HasPartner()
    {
        _repo.Setup(r => r.GetConversation(1)).ReturnsAsync(Response<ConversationInfoRecord>.Success(new ConversationInfoRecord { Id = 1 }));
        _repo.Setup(r => r.GetMembers(1)).ReturnsAsync(Response<List<User>>.Success(new()
        {
            TestData.User(TestData.MeId, "me"),
            TestData.User(TestData.OtherId, "bob")
        }));
        var service = CreateService();

        var chat = await service.GetChatAsync(1);

        Assert.NotNull(chat);
        Assert.False(chat!.IsGroup);
        Assert.Equal("bob", chat.Partner!.Username);
    }
}
