using Social.Core.Interfaces.Response;
using Social.Core.Models.Database;

namespace Social.Core.Interfaces.Services;

public interface IStoryService
{
    /// <summary>Nach Hinzufügen/Löschen einer eigenen Story.</summary>
    event Action? StoriesChanged;

    /// <summary>Gruppiert nach User: eigene zuerst, dann ungesehene, dann gesehene.</summary>
    Task<List<StoryGroup>> GetStoryGroupsAsync();

    /// <summary>Bild bereits verkleinert (JPEG).</summary>
    Task<IResponse> AddStoryAsync(byte[] imageData);

    Task<IResponse> DeleteStoryAsync(Story story);

    /// <summary>Markiert fremde Stories als gesehen; eigene werden ignoriert.</summary>
    Task MarkViewedAsync(Story story);

    Task<List<User>> GetViewersAsync(Story story);

    bool IsOwn(Story story);
}
