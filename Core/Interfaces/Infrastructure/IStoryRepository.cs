using Social.Core.Interfaces.Response;
using Social.Core.Models.Database;

namespace Social.Core.Interfaces.Infrastructure;

public interface IStoryRepository
{
    /// <summary>Aktive Stories von mir + gefolgten Accounts (View story_feed).</summary>
    public Task<IResponse<List<StoryFeedRecord>>> GetStories();

    public Task<IResponse<string>> UploadStoryImage(string userId, byte[] data, string contentType);

    public Task<IResponse> DeleteStoryImage(string publicUrl);

    public Task<IResponse<StoryRecord>> CreateStory(string userId, string imageUrl);

    public Task<IResponse> DeleteStory(long storyId);

    public Task<IResponse> MarkViewed(long storyId);

    public Task<IResponse<List<User>>> GetViewers(long storyId);
}
