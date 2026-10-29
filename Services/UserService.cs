using Social.Core.Interfaces.Infrastructure;
using Social.Core.Interfaces.Response;
using Social.Core.Interfaces.Services;
using Social.Core.Models.Database;
using Social.Core.Models.Response;

namespace Social.Services;

public class UserService(IUserRepository userRepository, IAuthService authService) : IUserService
{
    public event Action<User>? ProfileChanged;
    public event Action<string, bool>? FollowChanged;

    private string? CurrentUserId => authService.CurrentUser?.Id;

    public async Task<List<User>> SearchUsersAsync(string term)
    {
        term = term.Trim().TrimStart('@');
        if (term.Length == 0) return new List<User>();
        var result = await userRepository.SearchUsers(term, 20);
        return result is { IsSuccess: true, Data: not null } ? result.Data : new List<User>();
    }

    public async Task<bool> IsFollowingAsync(string userId)
    {
        if (CurrentUserId is not { } me || me == userId) return false;
        var result = await userRepository.IsFollowing(me, userId);
        return result is { IsSuccess: true, Data: true };
    }

    public async Task<IResponse> SetFollowAsync(User target, bool follow)
    {
        if (CurrentUserId is not { } me) return Response.Failure("Please sign in again.", null);
        if (me == target.Id) return Response.Failure("You can't follow yourself.", null);

        var result = await userRepository.SetFollow(me, target.Id, follow);
        if (!result.IsSuccess)
            return Response.Failure(follow ? "Could not follow. Please try again." : "Could not unfollow. Please try again.", result.Exception);

        target.FollowersCount = Math.Max(0, target.FollowersCount + (follow ? 1 : -1));
        FollowChanged?.Invoke(target.Id, follow);
        return Response.Success();
    }

    public async Task<List<User>> GetFollowersAsync(string userId)
    {
        var result = await userRepository.GetFollowers(userId);
        return result is { IsSuccess: true, Data: not null } ? result.Data : new List<User>();
    }

    public async Task<List<User>> GetFollowingAsync(string userId)
    {
        var result = await userRepository.GetFollowing(userId);
        return result is { IsSuccess: true, Data: not null } ? result.Data : new List<User>();
    }

    public async Task<User?> GetCurrentUserAsync()
    {
        var authUser = authService.CurrentUser ?? (await authService.GetCurrentUser()).Data;
        if (authUser?.Id is null) return null;

        var result = await userRepository.GetProfile(authUser.Id);
        return result.IsSuccess ? result.Data : null;
    }

    public async Task<User?> GetUserByUsernameAsync(string username)
    {
        var result = await userRepository.GetProfileByUsername(username.ToLowerInvariant());
        return result.IsSuccess ? result.Data : null;
    }

    public async Task<List<User>> GetSuggestedUsersAsync()
    {
        var result = await userRepository.GetSuggestedUsers(5);
        return result is { IsSuccess: true, Data: not null } ? result.Data : new List<User>();
    }

    public async Task<bool> IsUsernameTakenByOtherAsync(string username, string ownUserId)
    {
        var result = await userRepository.GetProfileByUsername(username);
        return result is { IsSuccess: true, Data: not null } && result.Data.Id != ownUserId;
    }

    public async Task<IResponse<User>> UpdateProfileAsync(User profile, byte[]? avatarData = null, string? avatarContentType = null)
    {
        var previousAvatarUrl = profile.ProfileImageUrl;

        if (avatarData is not null)
        {
            var upload = await userRepository.UploadAvatar(profile.Id, avatarData, avatarContentType ?? "image/jpeg");
            if (upload is not { IsSuccess: true, Data: not null })
                return Response<User>.Failure("The profile picture could not be uploaded.");

            profile.ProfileImageUrl = upload.Data;
        }

        profile.DisplayName = $"{profile.Vorname} {profile.Nachname}".Trim();

        var update = await userRepository.UpdateProfile(profile);
        if (!update.IsSuccess)
        {
            // Neues Bild wurde nicht übernommen – nicht als Datei-Leiche im Storage lassen
            if (avatarData is not null)
                await userRepository.DeleteAvatar(profile.ProfileImageUrl);
            return Response<User>.Failure("Your profile could not be saved. Please try again.");
        }

        // Altes Profilbild erst nach erfolgreichem Speichern entfernen (best effort, Fehler sind unkritisch)
        if (avatarData is not null && !string.IsNullOrEmpty(previousAvatarUrl) && previousAvatarUrl != profile.ProfileImageUrl)
            await userRepository.DeleteAvatar(previousAvatarUrl);

        ProfileChanged?.Invoke(profile);
        return Response<User>.Success(profile);
    }
}
