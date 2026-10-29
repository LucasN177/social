using System.Text.RegularExpressions;
using Social.Core.Interfaces.Infrastructure;
using Social.Core.Interfaces.Response;
using Social.Core.Interfaces.Services;
using Social.Core.Mapping;
using Social.Core.Models.Database;
using Social.Core.Models.Response;

namespace Social.Services;

public class PostService(IPostRepository postRepository, IAuthService authService, IUserService userService) : IPostService
{
    private static readonly Regex HashtagRegex = new(@"#(\w{1,50})", RegexOptions.Compiled);

    public event Action<long>? PostCreated;
    public event Action<long>? PostDeleted;

    private string? CurrentUserId => authService.CurrentUser?.Id;

    public async Task<List<Post>> GetFeedAsync(int page)
    {
        var result = await postRepository.GetFeed(page * IPostService.PageSize, IPostService.PageSize);
        return ToPosts(result);
    }

    public async Task<List<Post>> GetHomeFeedAsync(int page)
    {
        var result = await postRepository.GetHomeFeed(page * IPostService.PageSize, IPostService.PageSize);
        return ToPosts(result);
    }

    public async Task<List<Post>> GetPostsByTagAsync(string tag, int page)
    {
        var result = await postRepository.GetPostsByTag(NormalizeTag(tag), page * IPostService.PageSize, IPostService.PageSize);
        return ToPosts(result);
    }

    public async Task<List<HashtagResult>> SearchHashtagsAsync(string term)
    {
        term = NormalizeTag(term);
        if (term.Length == 0) return new List<HashtagResult>();
        var result = await postRepository.SearchHashtags(term, 20);
        return result is { IsSuccess: true, Data: not null } ? result.Data : new List<HashtagResult>();
    }

    /// <summary>Tags werden beim Erstellen kleingeschrieben und ohne "#" gespeichert.</summary>
    public static string NormalizeTag(string tag) => tag.Trim().TrimStart('#').ToLowerInvariant();

    public async Task<List<Post>> GetUserPostsAsync(string userId, int page)
    {
        var result = await postRepository.GetUserPosts(userId, page * IPostService.PageSize, IPostService.PageSize);
        return ToPosts(result);
    }

    public async Task<List<Post>> GetSavedPostsAsync(int page)
    {
        var result = await postRepository.GetBookmarkedPosts(page * IPostService.PageSize, IPostService.PageSize);
        return ToPosts(result);
    }

    public async Task<Post?> GetPostAsync(long id)
    {
        var result = await postRepository.GetPost(id);
        return result is { IsSuccess: true, Data: not null } ? result.Data.ToPost() : null;
    }

    public async Task<IResponse<long>> CreatePostAsync(string content, string location, IReadOnlyList<PostImageUpload> images)
    {
        var userId = CurrentUserId;
        if (userId is null)
            return Response<long>.Failure("Please sign in again.");

        content = content.Trim();
        if (content.Length == 0 && images.Count == 0)
            return Response<long>.Failure("Add a photo or write something.");
        if (content.Length > IPostService.MaxContentLength)
            return Response<long>.Failure($"Your post is too long (max. {IPostService.MaxContentLength} characters).");
        if (images.Count > IPostService.MaxImages)
            return Response<long>.Failure($"You can add up to {IPostService.MaxImages} photos.");

        var uploadedUrls = new List<string>();
        foreach (var image in images)
        {
            var upload = await postRepository.UploadPostImage(userId, image.Data, image.ContentType);
            if (upload is not { IsSuccess: true, Data: not null })
            {
                await postRepository.DeletePostImages(uploadedUrls);
                return Response<long>.Failure("A photo could not be uploaded. Please try again.");
            }
            uploadedUrls.Add(upload.Data);
        }

        var tags = HashtagRegex.Matches(content)
            .Select(m => m.Groups[1].Value.ToLowerInvariant())
            .Distinct()
            .Take(30)
            .ToList();

        var result = await postRepository.CreatePost(content, location.Trim(), tags, uploadedUrls);
        if (!result.IsSuccess)
        {
            // Post wurde nicht angelegt – hochgeladene Bilder wieder entfernen
            await postRepository.DeletePostImages(uploadedUrls);
            return Response<long>.Failure("Your post could not be published. Please try again.");
        }

        PostCreated?.Invoke(result.Data);
        return result;
    }

    public async Task<IResponse> DeletePostAsync(Post post)
    {
        var result = await postRepository.DeletePost(post.Id);
        if (!result.IsSuccess)
            return Response.Failure("The post could not be deleted.", result.Exception);

        // Bilder erst nach dem Löschen des Posts entfernen (best effort)
        await postRepository.DeletePostImages(post.ImageUrls);
        PostDeleted?.Invoke(post.Id);
        return Response.Success();
    }

    public async Task<IResponse> ToggleLikeAsync(Post post)
    {
        if (CurrentUserId is not { } userId) return Response.Failure("Please sign in again.", null);

        post.IsLiked = !post.IsLiked;
        post.LikesCount = Math.Max(0, post.LikesCount + (post.IsLiked ? 1 : -1));

        var result = await postRepository.SetLike(post.Id, userId, post.IsLiked);
        if (!result.IsSuccess)
        {
            post.IsLiked = !post.IsLiked;
            post.LikesCount = Math.Max(0, post.LikesCount + (post.IsLiked ? 1 : -1));
        }
        return result;
    }

    public async Task<IResponse> ToggleBookmarkAsync(Post post)
    {
        if (CurrentUserId is not { } userId) return Response.Failure("Please sign in again.", null);

        post.IsBookmarked = !post.IsBookmarked;
        var result = await postRepository.SetBookmark(post.Id, userId, post.IsBookmarked);
        if (!result.IsSuccess)
            post.IsBookmarked = !post.IsBookmarked;
        return result;
    }

    public async Task<List<Comment>> GetCommentsAsync(long postId)
    {
        var result = await postRepository.GetComments(postId);
        return result is { IsSuccess: true, Data: not null } ? result.Data.Select(c => c.ToComment()).ToList() : new List<Comment>();
    }

    public async Task<IResponse<Comment>> AddCommentAsync(Post post, string content)
    {
        if (CurrentUserId is not { } userId) return Response<Comment>.Failure("Please sign in again.");

        content = content.Trim();
        if (content.Length is 0 or > 2000)
            return Response<Comment>.Failure("Comments can have 1 to 2000 characters.");

        var result = await postRepository.AddComment(post.Id, userId, content);
        if (result is not { IsSuccess: true, Data: not null })
            return Response<Comment>.Failure("Your comment could not be posted.");

        post.CommentsCount++;
        var author = await userService.GetCurrentUserAsync() ?? new User { Id = userId };
        return Response<Comment>.Success(new Comment
        {
            Id = result.Data.Id,
            PostId = post.Id,
            User = author,
            Content = content,
            CreatedAt = DateTime.Now
        });
    }

    public async Task<IResponse> DeleteCommentAsync(Post post, Comment comment)
    {
        var result = await postRepository.DeleteComment(comment.Id);
        if (result.IsSuccess)
            post.CommentsCount = Math.Max(0, post.CommentsCount - 1);
        return result;
    }

    public bool IsOwnPost(Post post) => post.UserId == CurrentUserId;

    public bool IsOwnComment(Comment comment) => comment.User.Id == CurrentUserId;

    private static List<Post> ToPosts(IResponse<List<FeedPostRecord>> result) =>
        result is { IsSuccess: true, Data: not null } ? result.Data.Select(r => r.ToPost()).ToList() : new List<Post>();
}
