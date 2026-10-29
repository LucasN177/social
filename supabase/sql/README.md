# Supabase SQL-Skripte

Alle Datenbankänderungen für Supabase liegen in diesem Ordner.

## Ausführen

1. Supabase Dashboard → **SQL Editor** öffnen.
2. Skripte **in Nummernreihenfolge** ausführen (`000_…`, `001_…`, …).
3. Alle Skripte sind idempotent geschrieben (`if not exists`, `create or replace`, `drop policy if exists`) und können gefahrlos erneut ausgeführt werden.

Alternativ mit der Supabase CLI:

```bash
for f in supabase/sql/*.sql; do psql "$SUPABASE_DB_URL" -f "$f"; done
```

## Regeln

- **Neue Änderungen = neues Skript** mit der nächsten freien Nummer (`012_…sql`). Bereits ausgeführte Skripte nicht nachträglich ändern.
- Dateiname: `NNN_kurze_beschreibung.sql` in `snake_case`.
- Jede neue Tabelle bekommt **RLS** und passende Policies im selben Skript.
- Tabellen/Spalten in `snake_case`; die C#-Modelle mappen per `[Table]` / `[Column]`.

## Übersicht

| Skript | Inhalt |
|---|---|
| `000_extensions.sql` | Extensions, Hilfsfunktion `set_updated_at()` |
| `001_users.sql` | Profil-Tabelle `users`, Trigger beim Registrieren |
| `002_posts.sql` | `posts`, `post_images` |
| `003_comments.sql` | `comments`, `comment_likes` |
| `004_interactions.sql` | `likes`, `bookmarks` |
| `005_follows.sql` | `follows` |
| `006_stories.sql` | `stories`, `story_views` |
| `007_notifications.sql` | `notifications` + Trigger für Like/Kommentar/Follow |
| `008_counters.sql` | Zähler-Trigger (Likes, Kommentare, Posts, Follower) |
| `009_views_and_functions.sql` | View `feed_posts`, RPC `suggested_users` |
| `010_storage.sql` | Storage-Buckets + Policies |
| `011_realtime.sql` | Realtime für `notifications` |
| `012_users_profile_rules.sql` | Nur Profilfelder für Clients änderbar, Format-Checks für Username/Bio |
| `013_posts_api.sql` | RPC `create_post` (Post + Bilder atomar), View `comment_details`, Feed um Vor-/Nachname erweitert |
