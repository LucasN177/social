using Social.core.Models;

namespace Social.Core.Interfaces.Services;

public interface IPostService
{
    Task<List<Post>> GetFeedPostsAsync();
    Task<Post> GetPostByIdAsync(int id);
    Task<bool> ToggleLikeAsync(int postId);
    Task<bool> ToggleBookmarkAsync(int postId);
    Task<List<Comment>> GetCommentsAsync(int postId);
    Task<Comment> AddCommentAsync(int postId, string content);    
}