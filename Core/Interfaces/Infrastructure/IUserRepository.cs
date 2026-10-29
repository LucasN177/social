using Social.Core.Interfaces.Response;
using Social.Core.Models.Database;

namespace Social.Core.Interfaces.Infrastructure;

public interface IUserRepository
{
    public Task<IResponse<User>> GetProfile(string id);

    public Task<IResponse<List<User>>> GetSuggestedUsers(int count);

    public Task<IResponse<bool>> IsUsernameTaken(string username);
}
