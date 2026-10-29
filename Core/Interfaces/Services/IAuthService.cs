using Social.Core.Interfaces.Response;
using Supabase.Gotrue;
using User = Supabase.Gotrue.User;

namespace Social.Core.Interfaces.Services;

public interface IAuthService
{
    public Session? CurrentSession { get; set; }
    public User? CurrentUser { get; set; }
    public Task<IResponse<User?>> GetCurrentUser();

    public Task<IResponse<Session?>> GetCurrentSession();

    public Task<IResponse<Session>> Login(string username, string password);

    public Task<IResponse<Session>> Register(string username, string password, Models.Database.User profile);

    public Task<IResponse<bool>> IsUsernameTaken(string username);

    public Task<IResponse> Logout();
}
