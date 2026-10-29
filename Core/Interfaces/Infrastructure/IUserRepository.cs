using Social.Core.Interfaces.Response;
using Social.Core.Models.Database;

namespace Social.Core.Interfaces.Infrastructure;

public interface IUserRepository
{
    public Task<IResponse<User>> GetProfile(string id);

    public Task<IResponse<User>> GetProfileByUsername(string username);

    public Task<IResponse<List<User>>> GetSuggestedUsers(int count);

    public Task<IResponse<bool>> IsUsernameTaken(string username);

    public Task<IResponse> UpdateProfile(User user);

    /// <summary>Lädt ein Profilbild in den Bucket "avatars" und gibt die öffentliche URL zurück.</summary>
    public Task<IResponse<string>> UploadAvatar(string userId, byte[] data, string contentType);

    /// <summary>Löscht ein Profilbild aus dem Bucket "avatars"; fremde URLs werden ignoriert.</summary>
    public Task<IResponse> DeleteAvatar(string publicUrl);
}
