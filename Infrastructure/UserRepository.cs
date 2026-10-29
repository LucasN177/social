using Social.Core.Interfaces.Infrastructure;
using Social.Core.Interfaces.Response;
using Social.Core.Models.Database;
using Social.Core.Models.Response;

namespace Social.Infrastructure;

public class UserRepository(Supabase.Client client) : IUserRepository
{
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
}
