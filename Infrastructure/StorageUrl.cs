namespace Social.Infrastructure;

public static class StorageUrl
{
    /// <summary>
    /// Ermittelt aus einer öffentlichen Supabase-Storage-URL den Pfad im angegebenen Bucket.
    /// Liefert false für fremde URLs (z. B. externe Bilder), damit diese nie gelöscht werden.
    /// </summary>
    public static bool TryGetPath(string? publicUrl, string bucket, out string path)
    {
        path = string.Empty;
        if (string.IsNullOrEmpty(publicUrl)) return false;

        var marker = $"/storage/v1/object/public/{bucket}/";
        var index = publicUrl.IndexOf(marker, StringComparison.Ordinal);
        if (index < 0) return false;

        path = Uri.UnescapeDataString(publicUrl[(index + marker.Length)..].Split('?')[0]);
        return path.Length > 0;
    }
}
