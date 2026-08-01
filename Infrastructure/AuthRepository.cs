using Social.Core.Interfaces.Infrastructure;
using Social.Core.Interfaces.Response;
using Social.Core.Models;
using Social.Core.Models.Database;
using Social.Core.Models.Response;
using Supabase.Gotrue;
using Task = System.Threading.Tasks.Task;

namespace Social.Infrastructure;

public class AuthRepository(Supabase.Client client): IAuthRepository
{
    public async Task<IResponse<Session>> Login(string username, string password)
    {
        var result = await client.Auth.SignInWithPassword(username, password);
        return result is not null ? Response<Session>.Success(result) : Response<Session>.Failure("Login failed");
    }

    public async Task<IResponse<Session>> Register(string username, string password)
    {
        var result = await client.Auth.SignUp(username, password);
        return result is not null ? Response<Session>.Success(result) : Response<Session>.Failure("Registration failed");
    }

    public async Task<IResponse> Logout()
    {
        await client.Auth.SignOut();
        return Response.Success();
    }

    public Task<IResponse<Session>> GetCurrentSession()
    {
        try
        {
            return Task.FromResult(client.Auth.CurrentSession is not null ? Response<Session>.Success(client.Auth.CurrentSession) : Response<Session>.Failure("No login available"));
        }
        catch (Exception exception)
        {
            return Task.FromException<IResponse<Session>>(exception);
        }
    }

    public Task<IResponse<User>> GetCurrentUser()
    {
        try
        {
            return Task.FromResult(client.Auth.CurrentUser is not null ? Response<User>.Success(client.Auth.CurrentUser) : Response<User>.Failure("No login available"));
        }
        catch (Exception exception)
        {
            return Task.FromException<IResponse<User>>(exception);
        }
    }

    public async Task<IResponse> InsertUserMetadata(UserDto user)
    {
        try
        {
            user.UserId = client.Auth.CurrentUser.Id;
            await client.From<UserDto>()
                .Where(x => x.UserId == client.Auth.CurrentUser.Id)
                .Set(x => x.Username, user.Username)
                .Set(x => x.Vorname, user.Vorname)
                .Set(x =>x.Nachname, user.Nachname)
                .Update();
            return Response.Success();
        }
        catch (Exception ex)
        {
            return Response.Failure(ex.Message, ex);
        }
    }

    public Task<IResponse> UpdateUserMetadata(UserDto user)
    {
        throw new NotImplementedException();
    }

    public async Task<IResponse<UserDto>> GetUserMetadata(string id)
    {
        var result = await client.From<UserDto>().Where(x => x.UserId == id).Get();
        return result.Model is not null ? Response<UserDto>.Success(result.Model) : Response<UserDto>.Failure("No user found");
    }
}