using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Social.Core.Models.Database;

[Table("users")]
public class User : BaseModel
{
    [PrimaryKey("id")]
    public string Id { get; set; } = string.Empty;

    // null, solange der User noch keinen Username gewählt hat (z. B. Accounts von vor der Registrierungsseite)
    [Column("username")]
    public string? Username { get; set; }

    [Column("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    [Column("vorname")]
    public string Vorname { get; set; } = string.Empty;

    [Column("nachname")]
    public string Nachname { get; set; } = string.Empty;

    [Column("bio")]
    public string Bio { get; set; } = string.Empty;

    [Column("profile_image_url")]
    public string ProfileImageUrl { get; set; } = string.Empty;

    [Column("followers_count")]
    public int FollowersCount { get; set; }

    [Column("following_count")]
    public int FollowingCount { get; set; }

    [Column("posts_count")]
    public int PostsCount { get; set; }

    [Column("is_verified")]
    public bool IsVerified { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public string Initials =>
        string.Concat(new[] { Vorname, Nachname }.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => char.ToUpperInvariant(s[0])))
            is { Length: > 0 } initials ? initials : string.IsNullOrEmpty(Username) ? "?" : Username[..1].ToUpperInvariant();
}
