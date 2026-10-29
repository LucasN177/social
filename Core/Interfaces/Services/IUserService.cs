using Social.Core.Interfaces.Response;
using Social.Core.Models.Database;

namespace Social.Core.Interfaces.Services;

public interface IUserService
{
    /// <summary>Wird ausgelöst, nachdem der eingeloggte User sein Profil geändert hat.</summary>
    event Action<User>? ProfileChanged;

    /// <summary>Wird nach Folgen/Entfolgen ausgelöst (Ziel-User-ID, folgt jetzt ja/nein).</summary>
    event Action<string, bool>? FollowChanged;

    Task<User?> GetCurrentUserAsync();
    Task<User?> GetUserByUsernameAsync(string username);
    Task<List<User>> GetSuggestedUsersAsync();
    Task<List<User>> SearchUsersAsync(string term);
    Task<List<Story>> GetStoriesAsync();

    /// <summary>true, wenn der Username schon von einem anderen User belegt ist.</summary>
    Task<bool> IsUsernameTakenByOtherAsync(string username, string ownUserId);

    Task<IResponse<User>> UpdateProfileAsync(User profile, byte[]? avatarData = null, string? avatarContentType = null);

    Task<bool> IsFollowingAsync(string userId);

    /// <summary>Folgt bzw. entfolgt; passt FollowersCount von <paramref name="target"/> an.</summary>
    Task<IResponse> SetFollowAsync(User target, bool follow);

    Task<List<User>> GetFollowersAsync(string userId);
    Task<List<User>> GetFollowingAsync(string userId);
}
