using Social.Core.Interfaces.Infrastructure;
using Social.Core.Interfaces.Response;
using Social.Core.Interfaces.Services;
using Social.Core.Mapping;
using Social.Core.Models.Database;
using Social.Core.Models.Response;

namespace Social.Services;

public class StoryService(IStoryRepository storyRepository, IAuthService authService) : IStoryService
{
    public event Action? StoriesChanged;

    private string? CurrentUserId => authService.CurrentUser?.Id;

    public async Task<List<StoryGroup>> GetStoryGroupsAsync()
    {
        var result = await storyRepository.GetStories();
        if (result is not { IsSuccess: true, Data: not null }) return new List<StoryGroup>();

        var me = CurrentUserId;
        return result.Data
            .Select(r => r.ToStory())
            .GroupBy(s => s.User.Id)
            .Select(g => new StoryGroup(g.First().User, g.OrderBy(s => s.CreatedAt).ToList()))
            .OrderByDescending(g => g.User.Id == me)
            .ThenBy(g => g.AllViewed)
            .ThenByDescending(g => g.Stories.Max(s => s.CreatedAt))
            .ToList();
    }

    public async Task<IResponse<int>> AddStoriesAsync(IReadOnlyList<StoryUpload> uploads)
    {
        if (CurrentUserId is not { } userId) return Response<int>.Failure("Please sign in again.");
        if (uploads.Count == 0) return Response<int>.Failure("Choose a photo or video.");
        if (uploads.Count > IStoryService.MaxItemsPerUpload)
            return Response<int>.Failure($"You can add up to {IStoryService.MaxItemsPerUpload} photos or videos at once.");
        if (uploads.Any(u => u.Data.Length == 0 || u.Data.Length > IStoryService.MaxVideoBytes))
            return Response<int>.Failure("Videos can be up to 50 MB.");

        // Nacheinander, damit die Reihenfolge der Auswahl erhalten bleibt (created_at)
        var published = 0;
        string? error = null;
        foreach (var upload in uploads)
        {
            var stored = await storyRepository.UploadStoryMedia(userId, upload.Data, upload.ContentType);
            if (stored is not { IsSuccess: true, Data: not null })
            {
                error = upload.IsVideo ? "A video could not be uploaded." : "A photo could not be uploaded.";
                break;
            }

            var created = await storyRepository.CreateStory(userId, stored.Data, upload.IsVideo ? "video" : "image");
            if (!created.IsSuccess)
            {
                await storyRepository.DeleteStoryMedia(stored.Data);
                error = "Your story could not be published.";
                break;
            }
            published++;
        }

        if (published > 0) StoriesChanged?.Invoke();

        if (error is null) return Response<int>.Success(published);
        // Feste Texte (keine Zahlen), damit sie sich übersetzen lassen (Schlüssel = englischer Text)
        return Response<int>.Failure(published > 0 ? "Some items could not be added. The others are in your story." : error);
    }

    public async Task<IResponse> DeleteStoryAsync(Story story)
    {
        var result = await storyRepository.DeleteStory(story.Id);
        if (!result.IsSuccess)
            return Response.Failure("The story could not be deleted.", result.Exception);

        await storyRepository.DeleteStoryMedia(story.ImageUrl);
        StoriesChanged?.Invoke();
        return Response.Success();
    }

    public async Task MarkViewedAsync(Story story)
    {
        if (story.IsViewed || IsOwn(story)) return;
        story.IsViewed = true;
        await storyRepository.MarkViewed(story.Id);
    }

    public async Task<List<User>> GetViewersAsync(Story story)
    {
        var result = await storyRepository.GetViewers(story.Id);
        return result is { IsSuccess: true, Data: not null } ? result.Data : new List<User>();
    }

    public async Task<IResponse<long>> ReplyAsync(Story story, string content)
    {
        content = content.Trim();
        if (IsOwn(story)) return Response<long>.Failure("You can't reply to your own story.");
        if (content.Length is 0 or > 2000) return Response<long>.Failure("Replies can have 1 to 2000 characters.");

        var result = await storyRepository.ReplyToStory(story.Id, content);
        return result.IsSuccess ? result : Response<long>.Failure("Your reply could not be sent.");
    }

    public bool IsOwn(Story story) => story.User.Id == CurrentUserId;
}
