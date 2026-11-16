using Social.Core.Interfaces.Response;
using Social.Core.Models.Database;

namespace Social.Core.Interfaces.Services;

public interface IStoryService
{
    /// <summary>So viele Bilder/Videos lassen sich auf einmal hinzufügen.</summary>
    const int MaxItemsPerUpload = 10;
    /// <summary>Entspricht dem Limit des Buckets "stories" (019_stories_media_replies_cleanup.sql).</summary>
    const long MaxVideoBytes = 50 * 1024 * 1024;
    /// <summary>Längere Videos werden nach dieser Zeit weitergeblättert.</summary>
    const int MaxVideoSeconds = 60;

    /// <summary>Nach Hinzufügen/Löschen einer eigenen Story.</summary>
    event Action? StoriesChanged;

    /// <summary>Gruppiert nach User: eigene zuerst, dann ungesehene, dann gesehene.</summary>
    Task<List<StoryGroup>> GetStoryGroupsAsync();

    /// <summary>Lädt die Dateien nacheinander hoch und legt je Datei eine Story an (Bilder bereits verkleinert).
    /// Liefert die Anzahl der veröffentlichten Stories; schlägt eine fehl, bleiben die vorherigen erhalten.</summary>
    Task<IResponse<int>> AddStoriesAsync(IReadOnlyList<StoryUpload> uploads);

    Task<IResponse> DeleteStoryAsync(Story story);

    /// <summary>Markiert fremde Stories als gesehen; eigene werden ignoriert.</summary>
    Task MarkViewedAsync(Story story);

    Task<List<User>> GetViewersAsync(Story story);

    /// <summary>Schickt die Antwort als Direktnachricht an den Autor. Liefert die ID der Unterhaltung.</summary>
    Task<IResponse<long>> ReplyAsync(Story story, string content);

    bool IsOwn(Story story);
}

/// <summary>Eine Datei für die Story: Bild (JPEG, verkleinert) oder Video.</summary>
public record StoryUpload(byte[] Data, string ContentType)
{
    public bool IsVideo => ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase);
}
