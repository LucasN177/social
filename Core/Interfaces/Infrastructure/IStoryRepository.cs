using Social.Core.Interfaces.Response;
using Social.Core.Models.Database;

namespace Social.Core.Interfaces.Infrastructure;

public interface IStoryRepository
{
    /// <summary>Aktive Stories von mir + gefolgten Accounts (View story_feed).</summary>
    public Task<IResponse<List<StoryFeedRecord>>> GetStories();

    /// <summary>Lädt ein Bild oder Video in den Bucket "stories" hoch und liefert die öffentliche URL.</summary>
    public Task<IResponse<string>> UploadStoryMedia(string userId, byte[] data, string contentType);

    public Task<IResponse> DeleteStoryMedia(string publicUrl);

    /// <param name="mediaType">"image" oder "video"</param>
    public Task<IResponse<StoryRecord>> CreateStory(string userId, string mediaUrl, string mediaType);

    public Task<IResponse> DeleteStory(long storyId);

    public Task<IResponse> MarkViewed(long storyId);

    public Task<IResponse<List<User>>> GetViewers(long storyId);

    /// <summary>RPC reply_to_story (019): Direktnachricht an den Autor, liefert die ID der Unterhaltung.</summary>
    public Task<IResponse<long>> ReplyToStory(long storyId, string content);
}
