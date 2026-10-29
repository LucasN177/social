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

    public async Task<IResponse> AddStoryAsync(byte[] imageData)
    {
        if (CurrentUserId is not { } userId) return Response.Failure("Please sign in again.", null);

        var upload = await storyRepository.UploadStoryImage(userId, imageData, "image/jpeg");
        if (upload is not { IsSuccess: true, Data: not null })
            return Response.Failure("Your story could not be uploaded.", upload.Exception);

        var created = await storyRepository.CreateStory(userId, upload.Data);
        if (!created.IsSuccess)
        {
            await storyRepository.DeleteStoryImage(upload.Data);
            return Response.Failure("Your story could not be published.", created.Exception);
        }

        StoriesChanged?.Invoke();
        return Response.Success();
    }

    public async Task<IResponse> DeleteStoryAsync(Story story)
    {
        var result = await storyRepository.DeleteStory(story.Id);
        if (!result.IsSuccess)
            return Response.Failure("The story could not be deleted.", result.Exception);

        await storyRepository.DeleteStoryImage(story.ImageUrl);
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

    public bool IsOwn(Story story) => story.User.Id == CurrentUserId;
}
