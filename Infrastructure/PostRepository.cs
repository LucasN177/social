using Social.Core.Interfaces.Infrastructure;
using Social.Core.Interfaces.Response;
using Social.Core.Models.Database;
using Social.Core.Models.Response;
using static Supabase.Postgrest.Constants;
using FileOptions = Supabase.Storage.FileOptions;

namespace Social.Infrastructure;

public class PostRepository(Supabase.Client client) : IPostRepository
{
    private const string ImageBucket = "post-images";
    private const string ReelBucket = "reels";

    public async Task<IResponse<List<FeedPostRecord>>> GetFeed(int offset, int limit)
    {
        try
        {
            var result = await client.From<FeedPostRecord>()
                .Order(x => x.CreatedAt, Ordering.Descending)
                .Range(offset, offset + limit - 1)
                .Get();
            return Response<List<FeedPostRecord>>.Success(result.Models);
        }
        catch (Exception ex)
        {
            return Response<List<FeedPostRecord>>.Failure(ex);
        }
    }

    public async Task<IResponse<List<FeedPostRecord>>> GetHomeFeed(int offset, int limit)
    {
        try
        {
            var result = await client.From<HomeFeedRecord>()
                .Order(x => x.CreatedAt, Ordering.Descending)
                .Range(offset, offset + limit - 1)
                .Get();
            return Response<List<FeedPostRecord>>.Success(result.Models.Cast<FeedPostRecord>().ToList());
        }
        catch (Exception ex)
        {
            return Response<List<FeedPostRecord>>.Failure(ex);
        }
    }

    public async Task<IResponse<List<FeedPostRecord>>> GetPostsByTag(string tag, int offset, int limit)
    {
        try
        {
            var result = await client.From<FeedPostRecord>()
                .Filter("tags", Operator.Contains, new List<object> { tag })
                .Order(x => x.CreatedAt, Ordering.Descending)
                .Range(offset, offset + limit - 1)
                .Get();
            return Response<List<FeedPostRecord>>.Success(result.Models);
        }
        catch (Exception ex)
        {
            return Response<List<FeedPostRecord>>.Failure(ex);
        }
    }

    public async Task<IResponse<List<HashtagResult>>> SearchHashtags(string term, int maxResults)
    {
        try
        {
            var result = await client.Rpc<List<HashtagResult>>("search_hashtags", new Dictionary<string, object>
            {
                ["term"] = term,
                ["max_results"] = maxResults
            });
            return Response<List<HashtagResult>>.Success(result ?? new List<HashtagResult>());
        }
        catch (Exception ex)
        {
            return Response<List<HashtagResult>>.Failure(ex);
        }
    }

    public async Task<IResponse<List<FeedPostRecord>>> GetUserPosts(string userId, int offset, int limit)
    {
        try
        {
            var result = await client.From<FeedPostRecord>()
                .Where(x => x.UserId == userId)
                .Order(x => x.CreatedAt, Ordering.Descending)
                .Range(offset, offset + limit - 1)
                .Get();
            return Response<List<FeedPostRecord>>.Success(result.Models);
        }
        catch (Exception ex)
        {
            return Response<List<FeedPostRecord>>.Failure(ex);
        }
    }

    public async Task<IResponse<List<FeedPostRecord>>> GetBookmarkedPosts(int offset, int limit)
    {
        try
        {
            var result = await client.From<FeedPostRecord>()
                .Filter("is_bookmarked", Operator.Equals, "true")
                .Order(x => x.CreatedAt, Ordering.Descending)
                .Range(offset, offset + limit - 1)
                .Get();
            return Response<List<FeedPostRecord>>.Success(result.Models);
        }
        catch (Exception ex)
        {
            return Response<List<FeedPostRecord>>.Failure(ex);
        }
    }

    public async Task<IResponse<FeedPostRecord>> GetPost(long id)
    {
        try
        {
            var result = await client.From<FeedPostRecord>().Where(x => x.Id == id).Get();
            return result.Model is not null ? Response<FeedPostRecord>.Success(result.Model) : Response<FeedPostRecord>.Failure("Post not found");
        }
        catch (Exception ex)
        {
            return Response<FeedPostRecord>.Failure(ex);
        }
    }

    public async Task<IResponse<long>> CreatePost(string content, string location, List<string> tags, List<string> imageUrls)
    {
        try
        {
            var id = await client.Rpc<long>("create_post", new Dictionary<string, object>
            {
                ["p_content"] = content,
                ["p_location"] = location,
                ["p_tags"] = tags,
                ["p_image_urls"] = imageUrls
            });
            return Response<long>.Success(id);
        }
        catch (Exception ex)
        {
            return Response<long>.Failure(ex);
        }
    }

    public async Task<IResponse> DeletePost(long id)
    {
        try
        {
            // RLS (posts_delete_own) erlaubt nur eigene Posts; Bilder, Likes, Kommentare folgen per "on delete cascade"
            await client.From<PostRecord>().Where(x => x.Id == id).Delete();
            return Response.Success();
        }
        catch (Exception ex)
        {
            return Response.Failure(ex.Message, ex);
        }
    }

    public async Task<IResponse<string>> UploadPostImage(string userId, byte[] data, string contentType)
    {
        try
        {
            // Pfad muss mit der User-ID beginnen (Policy media_insert_own_folder in 010_storage.sql)
            var extension = contentType == "image/png" ? "png" : "jpg";
            var path = $"{userId}/{Guid.NewGuid():N}.{extension}";

            var bucket = client.Storage.From(ImageBucket);
            await bucket.Upload(data, path, new FileOptions { ContentType = contentType });
            return Response<string>.Success(bucket.GetPublicUrl(path));
        }
        catch (Exception ex)
        {
            return Response<string>.Failure(ex);
        }
    }

    public async Task<IResponse> DeletePostImages(IEnumerable<string> publicUrls)
    {
        var urls = publicUrls.ToList();
        try
        {
            foreach (var bucket in new[] { ImageBucket, ReelBucket })
            {
                var paths = urls
                    .Select(url => StorageUrl.TryGetPath(url, bucket, out var path) ? path : null)
                    .OfType<string>()
                    .ToList();
                if (paths.Count > 0)
                    await client.Storage.From(bucket).Remove(paths);
            }
            return Response.Success();
        }
        catch (Exception ex)
        {
            return Response.Failure(ex.Message, ex);
        }
    }

    public async Task<IResponse<List<FeedPostRecord>>> GetVideoPosts(int offset, int limit)
    {
        try
        {
            var result = await client.From<FeedPostRecord>()
                .Filter("media_type", Operator.Equals, "video")
                .Order(x => x.CreatedAt, Ordering.Descending)
                .Range(offset, offset + limit - 1)
                .Get();
            return Response<List<FeedPostRecord>>.Success(result.Models);
        }
        catch (Exception ex)
        {
            return Response<List<FeedPostRecord>>.Failure(ex);
        }
    }

    public async Task<IResponse<string>> UploadReelVideo(string userId, byte[] data, string contentType)
    {
        try
        {
            var extension = contentType switch { "video/webm" => "webm", "video/quicktime" => "mov", _ => "mp4" };
            var path = $"{userId}/{Guid.NewGuid():N}.{extension}";
            var bucket = client.Storage.From(ReelBucket);
            await bucket.Upload(data, path, new FileOptions { ContentType = contentType });
            return Response<string>.Success(bucket.GetPublicUrl(path));
        }
        catch (Exception ex)
        {
            return Response<string>.Failure(ex);
        }
    }

    public async Task<IResponse<long>> CreateReel(string content, List<string> tags, string videoUrl)
    {
        try
        {
            var id = await client.Rpc<long>("create_reel", new Dictionary<string, object>
            {
                ["p_content"] = content,
                ["p_tags"] = tags,
                ["p_video_url"] = videoUrl
            });
            return Response<long>.Success(id);
        }
        catch (Exception ex)
        {
            return Response<long>.Failure(ex);
        }
    }

    public async Task<IResponse> SetLike(long postId, string userId, bool liked)
    {
        try
        {
            if (liked)
                await client.From<LikeRecord>().Insert(new LikeRecord { PostId = postId, UserId = userId });
            else
                await client.From<LikeRecord>().Where(x => x.PostId == postId && x.UserId == userId).Delete();
            return Response.Success();
        }
        catch (Exception ex)
        {
            return Response.Failure(ex.Message, ex);
        }
    }

    public async Task<IResponse> SetBookmark(long postId, string userId, bool bookmarked)
    {
        try
        {
            if (bookmarked)
                await client.From<BookmarkRecord>().Insert(new BookmarkRecord { PostId = postId, UserId = userId });
            else
                await client.From<BookmarkRecord>().Where(x => x.PostId == postId && x.UserId == userId).Delete();
            return Response.Success();
        }
        catch (Exception ex)
        {
            return Response.Failure(ex.Message, ex);
        }
    }

    public async Task<IResponse<List<CommentDetailRecord>>> GetComments(long postId)
    {
        try
        {
            var result = await client.From<CommentDetailRecord>()
                .Where(x => x.PostId == postId)
                .Order(x => x.CreatedAt, Ordering.Ascending)
                .Get();
            return Response<List<CommentDetailRecord>>.Success(result.Models);
        }
        catch (Exception ex)
        {
            return Response<List<CommentDetailRecord>>.Failure(ex);
        }
    }

    public async Task<IResponse<CommentRecord>> AddComment(long postId, string userId, string content)
    {
        try
        {
            var result = await client.From<CommentRecord>()
                .Insert(new CommentRecord { PostId = postId, UserId = userId, Content = content });
            return result.Model is not null ? Response<CommentRecord>.Success(result.Model) : Response<CommentRecord>.Failure("Comment not saved");
        }
        catch (Exception ex)
        {
            return Response<CommentRecord>.Failure(ex);
        }
    }

    public async Task<IResponse> DeleteComment(long commentId)
    {
        try
        {
            await client.From<CommentRecord>().Where(x => x.Id == commentId).Delete();
            return Response.Success();
        }
        catch (Exception ex)
        {
            return Response.Failure(ex.Message, ex);
        }
    }
}
