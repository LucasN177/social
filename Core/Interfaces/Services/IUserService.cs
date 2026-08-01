using Social.core.Models;

namespace Social.Interfaces;

public interface IUserService
{
    Task<User> GetCurrentUserAsync();
    Task<List<User>> GetSuggestedUsersAsync();
    Task<List<Story>> GetStoriesAsync();    
}