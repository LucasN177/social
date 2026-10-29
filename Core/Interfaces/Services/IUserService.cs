using Social.Core.Models.Database;

namespace Social.Core.Interfaces.Services;

public interface IUserService
{
    Task<User?> GetCurrentUserAsync();
    Task<List<User>> GetSuggestedUsersAsync();
    Task<List<Story>> GetStoriesAsync();    
}