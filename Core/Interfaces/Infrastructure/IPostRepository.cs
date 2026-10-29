using Social.Core.Interfaces.Response;
using Social.Core.Models.Database;

namespace Social.Core.Interfaces.Infrastructure;

public interface IPostRepository
{
    public Task<IResponse<List<FeedPostRecord>>> GetFeed(int offset, int limit);

    public Task<IResponse<List<FeedPostRecord>>> GetUserPosts(string userId, int offset, int limit);

    /// <summary>Vom eingeloggten User gespeicherte Posts (is_bookmarked bezieht sich auf auth.uid()).</summary>
    public Task<IResponse<List<FeedPostRecord>>> GetBookmarkedPosts(int offset, int limit);

    public Task<IResponse<FeedPostRecord>> GetPost(long id);

    /// <summary>Legt Post und Bilder über die RPC create_post (013_posts_api.sql) atomar an.</summary>
    public Task<IResponse<long>> CreatePost(string content, string location, List<string> tags, List<string> imageUrls);

    public Task<IResponse> DeletePost(long id);

    public Task<IResponse<string>> UploadPostImage(string userId, byte[] data, string contentType);

    /// <summary>Löscht Bilder aus dem Bucket "post-images"; fremde URLs werden ignoriert.</summary>
    public Task<IResponse> DeletePostImages(IEnumerable<string> publicUrls);

    public Task<IResponse> SetLike(long postId, string userId, bool liked);

    public Task<IResponse> SetBookmark(long postId, string userId, bool bookmarked);

    public Task<IResponse<List<CommentDetailRecord>>> GetComments(long postId);

    public Task<IResponse<CommentRecord>> AddComment(long postId, string userId, string content);

    public Task<IResponse> DeleteComment(long commentId);
}
