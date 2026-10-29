using Social.Core.Interfaces.Response;
using Social.Core.Models.Database;

namespace Social.Core.Interfaces.Services;

public interface IPostService
{
    const int PageSize = 10;
    const int MaxImages = 10;
    const int MaxContentLength = 2200;
    /// <summary>Entspricht dem Limit des Buckets "reels" (015_stories_reels.sql).</summary>
    const long MaxVideoBytes = 50 * 1024 * 1024;

    event Action<long>? PostCreated;
    event Action<long>? PostDeleted;

    /// <summary>Alle Posts (Explore).</summary>
    Task<List<Post>> GetFeedAsync(int page);
    /// <summary>Eigene Posts + Posts gefolgter Accounts.</summary>
    Task<List<Post>> GetHomeFeedAsync(int page);
    Task<List<Post>> GetPostsByTagAsync(string tag, int page);
    Task<List<HashtagResult>> SearchHashtagsAsync(string term);
    Task<List<Post>> GetReelsAsync(int page);

    /// <summary>Lädt das Video hoch und legt den Reel an (max. MaxVideoBytes).</summary>
    Task<IResponse<long>> CreateReelAsync(string content, byte[] video, string contentType);
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
