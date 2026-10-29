using Social.Core.Interfaces.Response;
using Supabase.Gotrue;
using User = Supabase.Gotrue.User;

namespace Social.Core.Interfaces.Infrastructure;

public interface IAuthRepository
{
    public Task<IResponse<Session>> Login(string username, string password);

    public Task<IResponse<Session>> Register(string username, string password, Models.Database.User profile);

    public Task<IResponse> Logout();

    public Task<IResponse<Session>> GetCurrentSession();

    public Task<IResponse<User>> GetCurrentUser();
}
