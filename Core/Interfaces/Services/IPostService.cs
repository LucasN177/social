using Social.Core.Interfaces.Response;
using Social.Core.Models.Database;

namespace Social.Core.Interfaces.Services;

public interface IPostService
{
    const int PageSize = 10;
    const int MaxImages = 10;
    const int MaxContentLength = 2200;

    event Action<long>? PostCreated;
    event Action<long>? PostDeleted;

    Task<List<Post>> GetFeedAsync(int page);
    Task<List<Post>> GetUserPostsAsync(string userId, int page);
    Task<List<Post>> GetSavedPostsAsync(int page);
    Task<Post?> GetPostAsync(long id);

    /// <summary>Lädt die Bilder hoch und legt den Post an. Bilder in Reihenfolge, bereits verkleinert.</summary>
    Task<IResponse<long>> CreatePostAsync(string content, string location, IReadOnlyList<PostImageUpload> images);
    Task<IResponse> DeletePostAsync(Post post);

    /// <summary>Schaltet sofort um (optimistisch) und setzt bei Fehler zurück.</summary>
    Task<IResponse> ToggleLikeAsync(Post post);
    Task<IResponse> ToggleBookmarkAsync(Post post);

    Task<List<Comment>> GetCommentsAsync(long postId);
    Task<IResponse<Comment>> AddCommentAsync(Post post, string content);
    Task<IResponse> DeleteCommentAsync(Post post, Comment comment);

    bool IsOwnPost(Post post);
    bool IsOwnComment(Comment comment);
}

public record PostImageUpload(byte[] Data, string ContentType);
