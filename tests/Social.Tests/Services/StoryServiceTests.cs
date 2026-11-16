using Moq;
using Social.Core.Interfaces.Infrastructure;
using Social.Core.Interfaces.Services;
using Social.Core.Models.Database;
using Social.Core.Models.Response;
using Social.Services;

namespace Social.Tests.Services;

public class StoryServiceTests
{
    private readonly Mock<IStoryRepository> _repo = new();

    private StoryService CreateService() => new(_repo.Object, TestData.AuthAs().Object);

    [Fact]
    public async Task AddStories_CreatesOneStoryPerFile_WithMediaType()
    {
        _repo.Setup(r => r.UploadStoryMedia(TestData.MeId, It.IsAny<byte[]>(), It.IsAny<string>()))
             .ReturnsAsync((string _, byte[] _, string type) => Response<string>.Success($"url-{type}"));
        _repo.Setup(r => r.CreateStory(TestData.MeId, It.IsAny<string>(), It.IsAny<string>()))
             .ReturnsAsync(Response<StoryRecord>.Success(new StoryRecord()));
        var service = CreateService();
        var changed = 0;
        service.StoriesChanged += () => changed++;

        var result = await service.AddStoriesAsync(new[]
        {
            new StoryUpload(new byte[] { 1 }, "image/jpeg"),
            new StoryUpload(new byte[] { 2 }, "video/mp4")
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data);
        Assert.Equal(1, changed);
        _repo.Verify(r => r.CreateStory(TestData.MeId, "url-image/jpeg", "image"), Times.Once);
        _repo.Verify(r => r.CreateStory(TestData.MeId, "url-video/mp4", "video"), Times.Once);
    }

    [Fact]
    public async Task AddStories_StopsAtFirstFailure_KeepsEarlierStories_AndCleansUp()
    {
        _repo.Setup(r => r.UploadStoryMedia(TestData.MeId, It.IsAny<byte[]>(), It.IsAny<string>()))
             .ReturnsAsync((string _, byte[] data, string _) => Response<string>.Success($"url-{data[0]}"));
        _repo.Setup(r => r.CreateStory(TestData.MeId, "url-1", It.IsAny<string>()))
             .ReturnsAsync(Response<StoryRecord>.Success(new StoryRecord()));
        _repo.Setup(r => r.CreateStory(TestData.MeId, "url-2", It.IsAny<string>()))
             .ReturnsAsync(Response<StoryRecord>.Failure("insert failed"));
        var service = CreateService();
        var changed = 0;
        service.StoriesChanged += () => changed++;

        var result = await service.AddStoriesAsync(new[]
        {
            new StoryUpload(new byte[] { 1 }, "image/jpeg"),
            new StoryUpload(new byte[] { 2 }, "image/jpeg"),
            new StoryUpload(new byte[] { 3 }, "image/jpeg")
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("1 of 3", result.ErrorMessage);
        Assert.Equal(1, changed);
        _repo.Verify(r => r.DeleteStoryMedia("url-2"), Times.Once);
        _repo.Verify(r => r.UploadStoryMedia(TestData.MeId, It.Is<byte[]>(d => d[0] == 3), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task AddStories_RejectsTooManyFiles()
    {
        var service = CreateService();
        var uploads = Enumerable.Range(0, IStoryService.MaxItemsPerUpload + 1)
            .Select(_ => new StoryUpload(new byte[] { 1 }, "image/jpeg")).ToList();

        var result = await service.AddStoriesAsync(uploads);

        Assert.False(result.IsSuccess);
        _repo.Verify(r => r.UploadStoryMedia(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Reply_RejectsOwnStory()
    {
        var service = CreateService();
        var story = new Story { Id = 5, User = new User { Id = TestData.MeId } };

        var result = await service.ReplyAsync(story, "nice");

        Assert.False(result.IsSuccess);
        _repo.Verify(r => r.ReplyToStory(It.IsAny<long>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Reply_SendsTrimmedText()
    {
        _repo.Setup(r => r.ReplyToStory(5, "nice")).ReturnsAsync(Response<long>.Success(9));
        var service = CreateService();
        var story = new Story { Id = 5, User = new User { Id = TestData.OtherId } };

        var result = await service.ReplyAsync(story, "  nice  ");

        Assert.True(result.IsSuccess);
        Assert.Equal(9, result.Data);
    }
}
