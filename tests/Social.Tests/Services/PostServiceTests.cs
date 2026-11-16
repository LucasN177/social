using Moq;
using Social.Core.Interfaces.Infrastructure;
using Social.Core.Interfaces.Response;
using Social.Core.Interfaces.Services;
using Social.Core.Models.Database;
using Social.Core.Models.Response;
using Social.Services;

namespace Social.Tests.Services;

public class PostServiceTests
{
    private readonly Mock<IPostRepository> _repo = new();
    private readonly Mock<IUserService> _users = new();

    private PostService CreateService(string? userId = TestData.MeId) =>
        new(_repo.Object, TestData.AuthAs(userId).Object, _users.Object);

    [Fact]
    public async Task UpdatePost_SavesTrimmedContent_AndReDetectsHashtags()
    {
        _repo.Setup(r => r.UpdatePost(42, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>()))
             .ReturnsAsync(Response.Success());
        var service = CreateService();
        var post = TestData.Post();
        Post? updated = null;
        service.PostUpdated += p => updated = p;

        var result = await service.UpdatePostAsync(post, "  New #Sunset and #sunset #Beach ", " Berlin ");

        Assert.True(result.IsSuccess);
        Assert.Equal("New #Sunset and #sunset #Beach", post.Content);
        Assert.Equal("Berlin", post.Location);
        Assert.Equal(new[] { "sunset", "beach" }, post.Tags);
        Assert.NotNull(post.EditedAt);
        Assert.Same(post, updated);
        _repo.Verify(r => r.UpdatePost(42, "New #Sunset and #sunset #Beach", "Berlin",
            It.Is<List<string>>(t => t.SequenceEqual(new[] { "sunset", "beach" }))), Times.Once);
    }

    [Fact]
    public async Task UpdatePost_RejectsPostsOfOtherUsers()
    {
        var service = CreateService();
        var post = TestData.Post(ownerId: TestData.OtherId);

        var result = await service.UpdatePostAsync(post, "hijacked", "");

        Assert.False(result.IsSuccess);
        Assert.Equal("Hello", post.Content);
        _repo.Verify(r => r.UpdatePost(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>()), Times.Never);
    }

    [Fact]
    public async Task UpdatePost_TextOnlyPostCannotBecomeEmpty()
    {
        var service = CreateService();
        var post = TestData.Post(images: 0);

        var result = await service.UpdatePostAsync(post, "   ", "");

        Assert.False(result.IsSuccess);
        Assert.Equal("Hello", post.Content);
    }

    [Fact]
    public async Task UpdatePost_KeepsPostUnchanged_WhenRepositoryFails()
    {
        _repo.Setup(r => r.UpdatePost(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>()))
             .ReturnsAsync(Response.Failure("{\"code\":\"42501\"}", new Exception("{\"code\":\"42501\"}")));
        var service = CreateService();
        var post = TestData.Post();

        var result = await service.UpdatePostAsync(post, "changed", "");

        Assert.False(result.IsSuccess);
        Assert.Equal("Hello", post.Content);
        Assert.Null(post.EditedAt);
        Assert.Equal("Your changes could not be saved. Please try again.", result.ErrorMessage);
    }

    [Fact]
    public async Task ToggleLike_RollsBack_WhenRepositoryFails()
    {
        _repo.Setup(r => r.SetLike(42, TestData.MeId, true)).ReturnsAsync(Response.Failure("offline", null));
        var service = CreateService();
        var post = TestData.Post();
        post.LikesCount = 3;

        var result = await service.ToggleLikeAsync(post);

        Assert.False(result.IsSuccess);
        Assert.False(post.IsLiked);
        Assert.Equal(3, post.LikesCount);
    }

    [Fact]
    public async Task ToggleCommentLike_IsOptimistic_AndCounts()
    {
        _repo.Setup(r => r.SetCommentLike(7, TestData.MeId, true)).ReturnsAsync(Response.Success());
        var service = CreateService();
        var comment = new Comment { Id = 7, LikesCount = 1 };

        var result = await service.ToggleCommentLikeAsync(comment);

        Assert.True(result.IsSuccess);
        Assert.True(comment.IsLiked);
        Assert.Equal(2, comment.LikesCount);
    }

    [Fact]
    public async Task ToggleCommentLike_RollsBack_WhenRepositoryFails()
    {
        _repo.Setup(r => r.SetCommentLike(7, TestData.MeId, false)).ReturnsAsync(Response.Failure("x", null));
        var service = CreateService();
        var comment = new Comment { Id = 7, LikesCount = 1, IsLiked = true };

        await service.ToggleCommentLikeAsync(comment);

        Assert.True(comment.IsLiked);
        Assert.Equal(1, comment.LikesCount);
    }

    [Fact]
    public async Task CreatePost_RemovesUploadedImages_WhenRpcFails()
    {
        var uploaded = new[] { "https://x/storage/v1/object/public/post-images/me/a.jpg", "https://x/storage/v1/object/public/post-images/me/b.jpg" };
        var call = 0;
        _repo.Setup(r => r.UploadPostImage(TestData.MeId, It.IsAny<byte[]>(), "image/jpeg"))
             .ReturnsAsync(() => Response<string>.Success(uploaded[call++]));
        _repo.Setup(r => r.CreatePost(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<List<string>>()))
             .ReturnsAsync(Response<long>.Failure("rpc failed"));
        var service = CreateService();

        var result = await service.CreatePostAsync("hi", "", new[] { new PostImageUpload(new byte[] { 1 }, "image/jpeg"), new PostImageUpload(new byte[] { 2 }, "image/jpeg") });

        Assert.False(result.IsSuccess);
        _repo.Verify(r => r.DeletePostImages(It.Is<IEnumerable<string>>(urls => urls.SequenceEqual(uploaded))), Times.Once);
    }

    [Fact]
    public async Task CreatePost_RequiresTextOrImage()
    {
        var service = CreateService();

        var result = await service.CreatePostAsync("   ", "", Array.Empty<PostImageUpload>());

        Assert.False(result.IsSuccess);
        _repo.Verify(r => r.CreatePost(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<List<string>>()), Times.Never);
    }

    [Theory]
    [InlineData("#Sunset", "sunset")]
    [InlineData("  sunset ", "sunset")]
    [InlineData("#BEACH", "beach")]
    public void NormalizeTag_LowercasesAndStripsHash(string input, string expected) =>
        Assert.Equal(expected, PostService.NormalizeTag(input));
}
