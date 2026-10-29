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

    public async Task<IResponse<string>> UploadStoryImage(string userId, byte[] data, string contentType)
    {
        try
        {
            // Pfad muss mit der User-ID beginnen (Policy media_insert_own_folder in 010_storage.sql)
            var path = $"{userId}/{Guid.NewGuid():N}.jpg";
            var bucket = client.Storage.From(Bucket);
            await bucket.Upload(data, path, new FileOptions { ContentType = contentType });
            return Response<string>.Success(bucket.GetPublicUrl(path));
        }
        catch (Exception ex)
        {
            return Response<string>.Failure(ex);
        }
    }

    public async Task<IResponse> DeleteStoryImage(string publicUrl)
    {
        if (!StorageUrl.TryGetPath(publicUrl, Bucket, out var path))
            return Response.Failure("Not a story image in our storage", null);

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

    public async Task<IResponse<StoryRecord>> CreateStory(string userId, string imageUrl)
    {
        try
        {
            // expires_at setzt die Datenbank (Default: jetzt + 24 h)
            var result = await client.From<StoryRecord>().Insert(new StoryRecord { UserId = userId, ImageUrl = imageUrl });
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
}
