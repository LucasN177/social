using Social.Core.Interfaces.Response;
using Social.Core.Models.Database;

namespace Social.Core.Interfaces.Services;

public interface IUserService
{
    /// <summary>Wird ausgelöst, nachdem der eingeloggte User sein Profil geändert hat.</summary>
    event Action<User>? ProfileChanged;

    Task<User?> GetCurrentUserAsync();
    Task<User?> GetUserByUsernameAsync(string username);
    Task<List<User>> GetSuggestedUsersAsync();
    Task<List<Story>> GetStoriesAsync();

    /// <summary>true, wenn der Username schon von einem anderen User belegt ist.</summary>
    Task<bool> IsUsernameTakenByOtherAsync(string username, string ownUserId);

    Task<IResponse<User>> UpdateProfileAsync(User profile, byte[]? avatarData = null, string? avatarContentType = null);
}
