using Social.Core.Interfaces.Response;
using Social.Core.Models.Database;
using Supabase.Gotrue;
using User = Supabase.Gotrue.User;

namespace Social.Core.Interfaces.Infrastructure;

public interface IAuthRepository
{
    public Task<IResponse<Session>> Login(string username, string password);
    
    public Task<IResponse<Session>> Register(string username, string password, UserDto profile);
    
    public Task<IResponse> Logout();
    
    public Task<IResponse<Session>> GetCurrentSession();
    
    public Task<IResponse<User>> GetCurrentUser();

    public Task<IResponse> InsertUserMetadata(UserDto user);
    
    public Task<IResponse> UpdateUserMetadata(UserDto user);
    
    public Task<IResponse<UserDto>> GetUserMetadata(string id);
    
}