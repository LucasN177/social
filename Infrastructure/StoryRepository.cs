using Social.Core.Interfaces.Infrastructure;
using Social.Core.Interfaces.Response;
using Social.Core.Models.Database;
using Social.Core.Models.Response;
using static Supabase.Postgrest.Constants;
using FileOptions = Supabase.Storage.FileOptions;

namespace Social.Infrastructure;

public class StoryRepository(Supabase.Client client) : IStoryRepository
{
    private const string Bucket = "stories";

    public async Task<IResponse<List<StoryFeedRecord>>> GetStories()
    {
        try
        {
            var result = await client.From<StoryFeedRecord>()
                .Order(x => x.CreatedAt, Ordering.Ascending)
                .Get();
            return Response<List<StoryFeedRecord>>.Success(result.Models);
        }
        catch (Exception ex)
        {
            return Response<List<StoryFeedRecord>>.Failure(ex);
        }
    }

    public async Task<IResponse<string>> UploadStoryMedia(string userId, byte[] data, string contentType)
    {
        try
        {
            // Pfad muss mit der User-ID beginnen (Policy media_insert_own_folder in 010_storage.sql)
            var extension = contentType switch
            {
                "video/mp4" => "mp4",
                "video/webm" => "webm",
                "video/quicktime" => "mov",
                "image/png" => "png",
                "image/webp" => "webp",
                _ => "jpg"
            };
            var path = $"{userId}/{Guid.NewGuid():N}.{extension}";
            var bucket = client.Storage.From(Bucket);
            await bucket.Upload(data, path, new FileOptions { ContentType = contentType });
            return Response<string>.Success(bucket.GetPublicUrl(path));
        }
        catch (Exception ex)
        {
            return Response<string>.Failure(ex);
        }
    }

    public async Task<IResponse> DeleteStoryMedia(string publicUrl)
    {
        if (!StorageUrl.TryGetPath(publicUrl, Bucket, out var path))
            return Response.Failure("Not a story file in our storage", null);

        try
        {
            await client.Storage.From(Bucket).Remove(path);
            return Response.Success();
        }
        catch (Exception ex)
        {
            return Response.Failure(ex.Message, ex);
        }
    }

    public async Task<IResponse<StoryRecord>> CreateStory(string userId, string mediaUrl, string mediaType)
    {
        try
        {
            // expires_at setzt die Datenbank (Default: jetzt + 24 h)
            var result = await client.From<StoryRecord>()
                .Insert(new StoryRecord { UserId = userId, ImageUrl = mediaUrl, MediaType = mediaType });
            return result.Model is not null ? Response<StoryRecord>.Success(result.Model) : Response<StoryRecord>.Failure("Story not saved");
        }
        catch (Exception ex)
        {
            return Response<StoryRecord>.Failure(ex);
        }
    }

    public async Task<IResponse> DeleteStory(long storyId)
    {
        try
        {
            await client.From<StoryRecord>().Where(x => x.Id == storyId).Delete();
            return Response.Success();
        }
        catch (Exception ex)
        {
            return Response.Failure(ex.Message, ex);
        }
    }

    public async Task<IResponse> MarkViewed(long storyId)
    {
        try
        {
            await client.Rpc("mark_story_viewed", new Dictionary<string, object> { ["p_story_id"] = storyId });
            return Response.Success();
        }
        catch (Exception ex)
        {
            return Response.Failure(ex.Message, ex);
        }
    }

    public async Task<IResponse<List<User>>> GetViewers(long storyId)
    {
        try
        {
            var result = await client.Rpc<List<User>>("story_viewers", new Dictionary<string, object> { ["p_story_id"] = storyId });
            return Response<List<User>>.Success(result ?? new List<User>());
        }
        catch (Exception ex)
        {
            return Response<List<User>>.Failure(ex);
        }
    }

    public async Task<IResponse<long>> ReplyToStory(long storyId, string content)
    {
        try
        {
            var conversationId = await client.Rpc<long>("reply_to_story", new Dictionary<string, object>
            {
                ["p_story_id"] = storyId,
                ["p_content"] = content
            });
            return Response<long>.Success(conversationId);
        }
        catch (Exception ex)
        {
            return Response<long>.Failure(ex);
        }
    }
}
