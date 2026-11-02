using Supabase.Postgrest.Attributes;
using Newtonsoft.Json;
using Supabase.Postgrest.Models;

namespace Social.Core.Models.Database;

// NULL-tolerant: Ältere users-Tabellen (vor supabase/sql) können NULL in Text- und Zählerspalten enthalten.
// Strings werden zu "", NULL bei Zahlen/Bool/Datum wird ignoriert (Standardwert bleibt) – sonst
// scheitert das Laden bzw. das Bearbeiten des Profils (NullReferenceException bei Trim()).
[Table("users")]
public class User : BaseModel
{
    private string _displayName = string.Empty;
    private string _vorname = string.Empty;
    private string _nachname = string.Empty;
    private string _bio = string.Empty;
    private string _profileImageUrl = string.Empty;

    [PrimaryKey("id"), JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    // null, solange der User noch keinen Username gewählt hat (z. B. Accounts von vor der Registrierungsseite)
    [Column("username"), JsonProperty("username")]
    public string? Username { get; set; }

    [Column("display_name"), JsonProperty("display_name")]
    public string DisplayName { get => _displayName; set => _displayName = value ?? string.Empty; }

    [Column("vorname"), JsonProperty("vorname")]
    public string Vorname { get => _vorname; set => _vorname = value ?? string.Empty; }

    [Column("nachname"), JsonProperty("nachname")]
    public string Nachname { get => _nachname; set => _nachname = value ?? string.Empty; }

    [Column("bio"), JsonProperty("bio")]
    public string Bio { get => _bio; set => _bio = value ?? string.Empty; }

    [Column("profile_image_url"), JsonProperty("profile_image_url")]
    public string ProfileImageUrl { get => _profileImageUrl; set => _profileImageUrl = value ?? string.Empty; }

    [Column("followers_count", NullValueHandling.Ignore), JsonProperty("followers_count", NullValueHandling = NullValueHandling.Ignore)]
    public int FollowersCount { get; set; }

    [Column("following_count", NullValueHandling.Ignore), JsonProperty("following_count", NullValueHandling = NullValueHandling.Ignore)]
    public int FollowingCount { get; set; }

    [Column("posts_count", NullValueHandling.Ignore), JsonProperty("posts_count", NullValueHandling = NullValueHandling.Ignore)]
    public int PostsCount { get; set; }

    [Column("is_verified", NullValueHandling.Ignore), JsonProperty("is_verified", NullValueHandling = NullValueHandling.Ignore)]
    public bool IsVerified { get; set; }

    [Column("created_at", NullValueHandling.Ignore), JsonProperty("created_at", NullValueHandling = NullValueHandling.Ignore)]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [JsonIgnore]
    public string Initials =>
        string.Concat(new[] { Vorname, Nachname }.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => char.ToUpperInvariant(s[0])))
            is { Length: > 0 } initials ? initials : string.IsNullOrEmpty(Username) ? "?" : Username[..1].ToUpperInvariant();
}
