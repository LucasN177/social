using Moq;
using Social.Core.Interfaces.Services;
using Social.Core.Models.Database;
using GotrueUser = Supabase.Gotrue.User;

namespace Social.Tests;

/// <summary>Gemeinsame Testdaten: eingeloggter User "me" und Hilfen für Mocks.</summary>
internal static class TestData
{
    public const string MeId = "11111111-1111-1111-1111-111111111111";
    public const string OtherId = "22222222-2222-2222-2222-222222222222";

    public static Mock<IAuthService> AuthAs(string? userId = MeId)
    {
        var auth = new Mock<IAuthService>();
        auth.SetupGet(a => a.CurrentUser).Returns(userId is null ? null : new GotrueUser { Id = userId });
        return auth;
    }

    public static Post Post(string ownerId = MeId, string content = "Hello", int images = 1) => new()
    {
        Id = 42,
        UserId = ownerId,
        User = new User { Id = ownerId, Username = "owner" },
        Content = content,
        ImageUrls = Enumerable.Range(0, images).Select(i => $"https://x.supabase.co/storage/v1/object/public/post-images/{ownerId}/{i}.jpg").ToList()
    };

    public static User User(string id, string username) => new() { Id = id, Username = username, DisplayName = username };
}
