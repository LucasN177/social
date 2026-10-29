using Social.Core.Interfaces.Infrastructure;
using Social.Core.Interfaces.Services;
using Social.Core.Models.Database;

namespace Social.Services;

public class UserService(IUserRepository userRepository, IAuthService authService) : IUserService
{
    public async Task<User?> GetCurrentUserAsync()
    {
        var authUser = authService.CurrentUser ?? (await authService.GetCurrentUser()).Data;
        if (authUser?.Id is null) return null;

        var result = await userRepository.GetProfile(authUser.Id);
        return result.IsSuccess ? result.Data : null;
    }

    public async Task<List<User>> GetSuggestedUsersAsync()
    {
        var result = await userRepository.GetSuggestedUsers(5);
        return result is { IsSuccess: true, Data: not null } ? result.Data : new List<User>();
    }

    public Task<List<Story>> GetStoriesAsync()
    {
        // Stories kommen mit Phase 4 aus Supabase (supabase/sql/006_stories.sql)
        return Task.FromResult(new List<Story>());
    }
}
