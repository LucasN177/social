using Social.Core.Interfaces.Infrastructure;
using Social.Core.Interfaces.Response;
using Social.Core.Models.Database;
using Social.Core.Models.Response;
using FileOptions = Supabase.Storage.FileOptions;

namespace Social.Infrastructure;

public class UserRepository(Supabase.Client client) : IUserRepository
{
    private const string AvatarBucket = "avatars";

    public async Task<IResponse<User>> GetProfile(string id)
    {
        try
        {
            var result = await client.From<User>().Where(x => x.Id == id).Single();
            return result is not null ? Response<User>.Success(result) : Response<User>.Failure("No profile found");
        }
        catch (Exception ex)
        {
            return Response<User>.Failure(ex);
        }
    }

    public async Task<IResponse<User>> GetProfileByUsername(string username)
    {
        try
        {
            var result = await client.From<User>().Where(x => x.Username == username).Get();
            return result.Model is not null ? Response<User>.Success(result.Model) : Response<User>.Failure("No profile found");
        }
        catch (Exception ex)
        {
            return Response<User>.Failure(ex);
        }
    }

    public async Task<IResponse<List<User>>> GetSuggestedUsers(int count)
    {
        try
        {
            // RPC aus supabase/sql/009_views_and_functions.sql
            var result = await client.Rpc<List<User>>("suggested_users", new Dictionary<string, object> { ["max_results"] = count });
            return Response<List<User>>.Success(result ?? new List<User>());
        }
        catch (Exception ex)
        {
            return Response<List<User>>.Failure(ex);
        }
    }

    public async Task<IResponse<bool>> IsUsernameTaken(string username)
    {
        try
        {
            var result = await client.From<User>().Where(x => x.Username == username).Get();
            return Response<bool>.Success(result.Models.Count > 0);
        }
        catch (Exception ex)
        {
            return Response<bool>.Failure(ex);
        }
    }

    public async Task<IResponse> UpdateProfile(User user)
    {
        try
        {
            // Nur die Profilfelder senden – Zähler und is_verified darf der Client nicht ändern (012_users_profile_rules.sql)
            await client.From<User>()
                .Where(x => x.Id == user.Id)
                .Set(x => x.Username!, user.Username!)
                .Set(x => x.DisplayName, user.DisplayName)
                .Set(x => x.Vorname, user.Vorname)
                .Set(x => x.Nachname, user.Nachname)
                .Set(x => x.Bio, user.Bio)
                .Set(x => x.ProfileImageUrl, user.ProfileImageUrl)
                .Update();
            return Response.Success();
        }
        catch (Exception ex)
        {
            return Response.Failure(ex.Message, ex);
        }
    }

    public async Task<IResponse<string>> UploadAvatar(string userId, byte[] data, string contentType)
    {
        try
        {
            // Pfad muss mit der User-ID beginnen (Policy media_insert_own_folder in 010_storage.sql).
            // Neuer Dateiname pro Upload, damit Browser-Caches das alte Bild nicht weiter anzeigen.
            var extension = contentType == "image/png" ? "png" : "jpg";
            var path = $"{userId}/avatar-{DateTime.UtcNow:yyyyMMddHHmmss}.{extension}";

            var bucket = client.Storage.From(AvatarBucket);
            await bucket.Upload(data, path, new FileOptions { ContentType = contentType, Upsert = true });
            return Response<string>.Success(bucket.GetPublicUrl(path));
        }
        catch (Exception ex)
        {
            return Response<string>.Failure(ex);
        }
    }
}
