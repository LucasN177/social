using Social.Core.Interfaces.Infrastructure;
using Social.Core.Interfaces.Response;
using Social.Core.Interfaces.Services;
using Social.Core.Models.Response;
using Supabase.Gotrue;
using User = Supabase.Gotrue.User;
using Task = System.Threading.Tasks.Task;

namespace Social.Services;

public class AuthService(IAuthRepository authRepository, IUserRepository userRepository) : IAuthService
{
    public Session? CurrentSession { get; set; }
    public User? CurrentUser { get; set; }

    public async Task<IResponse<User?>> GetCurrentUser()
    {
        var result =  await authRepository.GetCurrentUser();
        if (result.IsSuccess)
        {
            CurrentUser = result.Data;
            return Response<User?>.Success(result.Data);
        }
        return Response<User?>.Failure(result.ErrorMessage ?? "No current user");
    }

    public async Task<IResponse<Session?>> GetCurrentSession()
    {
        var result =  await authRepository.GetCurrentSession();
        if (result.IsSuccess)
        {
            CurrentSession = result.Data;
            return Response<Session?>.Success(result.Data);
        }
        return Response<Session?>.Failure(result.ErrorMessage ?? "No current session");
    }

    public async Task<IResponse<Session>> Login(string username, string password)
    {
        var result =  await authRepository.Login(username, password);
        if (result is not { IsSuccess: true, Data: not null })
            return Response<Session>.Failure(result.ErrorMessage ?? "Login failed");

        CurrentSession = result.Data;
        CurrentUser = result.Data.User;
        return Response<Session>.Success(result.Data);
    }

    public async Task<IResponse<Session>> Register(string username, string password, Core.Models.Database.User profile)
    {
        var result =  await authRepository.Register(username, password, profile);
        return result is { IsSuccess: true, Data: not null } ? Response<Session>.Success(result.Data) : Response<Session>.Failure(result.ErrorMessage ?? "Register failed.");
    }

    public async Task<IResponse<bool>> IsUsernameTaken(string username)
    {
        return await userRepository.IsUsernameTaken(username);
    }

    public async Task<IResponse> Logout()
    {
        CurrentSession = null;
        CurrentUser = null;
        return await authRepository.Logout();
    }
}
