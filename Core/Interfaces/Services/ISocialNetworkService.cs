using Social.Core.Models.Database;

namespace Social.Core.Interfaces.Services;

public interface ISocialNetworkService
{
    // Users
    Task<List<User>> GetUsersAsync();
    Task<User?> GetUserByIdAsync(int id);
    Task<User> CreateUserAsync(User user);
    Task<User> UpdateUserAsync(User user);
    Task<bool> DeleteUserAsync(int id);
    
    // Posts
    Task<List<Post>> GetPostsAsync(int? userId = null, int limit = 20, int offset = 0);
    Task<Post?> GetPostByIdAsync(int id);
    Task<Post> CreatePostAsync(Post post);
    Task<Post> UpdatePostAsync(Post post);
    Task<bool> DeletePostAsync(int id);
    
    // Comments
    Task<List<Comment>> GetCommentsAsync(int postId);
    Task<Comment> CreateCommentAsync(Comment comment);
    Task<bool> DeleteCommentAsync(int id);
    
    // Stories
    Task<List<Story>> GetStoriesAsync(int? userId = null);
    Task<Story> CreateStoryAsync(Story story);
    Task<bool> DeleteStoryAsync(int id);
    
    // Interactions
    Task<bool> LikePostAsync(int postId, int userId);
    Task<bool> UnlikePostAsync(int postId, int userId);
    Task<bool> BookmarkPostAsync(int postId, int userId);
    Task<bool> UnbookmarkPostAsync(int postId, int userId);
    Task<bool> FollowUserAsync(int followerId, int followingId);
    Task<bool> UnfollowUserAsync(int followerId, int followingId);
    Task<bool> ViewStoryAsync(int storyId, int userId);    
}