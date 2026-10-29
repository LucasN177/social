# Supabase SQL-Skripte

Alle Datenbankänderungen für Supabase liegen in diesem Ordner.

## Ausführen – empfohlen: ein einziges Skript

1. Supabase Dashboard → **SQL Editor** öffnen.
2. Den kompletten Inhalt von [`../setup.sql`](../setup.sql) einfügen und **Run** klicken.

`setup.sql` enthält alle Skripte dieses Ordners in Nummernreihenfolge und läuft in **einer Transaktion**: Schlägt etwas fehl, wird nichts übernommen. Es ist idempotent (`if not exists`, `create or replace`, `drop policy if exists`) und kann nach jedem Update einfach komplett erneut ausgeführt werden.

Alternativ mit `psql`:

```bash
psql "$SUPABASE_DB_URL" -f supabase/setup.sql
```

## setup.sql neu erzeugen

`setup.sql` wird **nicht von Hand bearbeitet**, sondern aus den nummerierten Skripten erzeugt:

```bash
powershell -ExecutionPolicy Bypass -File supabase/build-setup.ps1
```

## Regeln

- **Neue Änderungen = neues Skript** mit der nächsten freien Nummer (`015_…sql`), danach `setup.sql` neu erzeugen und beides committen.
- Dateiname: `NNN_kurze_beschreibung.sql` in `snake_case`.
- Jedes Skript muss **mehrfach ausführbar** sein. Achtung bei Views: `create or replace view` kann keine Spalten entfernen – wird eine View erweitert, muss jede frühere Definition dieselben Spalten haben (siehe `feed_posts` in 009/013).
- Jede neue Tabelle bekommt **RLS** und passende Policies im selben Skript.
- Tabellen/Spalten in `snake_case`; die C#-Modelle mappen per `[Table]` / `[Column]` (und `[JsonProperty]`, wenn sie auch aus RPCs gelesen werden).

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
| `009_views_and_functions.sql` | View `feed_posts`, RPCs `suggested_users`, `search_users` |
| `010_storage.sql` | Storage-Buckets + Policies |
| `011_realtime.sql` | Realtime für `notifications` |
| `012_users_profile_rules.sql` | Nur Profilfelder für Clients änderbar, Format-Checks für Username/Bio |
| `013_posts_api.sql` | RPC `create_post` (Post + Bilder atomar), View `comment_details`, Feed um Vor-/Nachname erweitert |
| `014_social_graph.sql` | View `home_feed` (eigene + gefolgte), RPCs `search_hashtags`, `list_followers`, `list_following` |
| `015_stories_reels.sql` | View `story_feed`, RPCs `mark_story_viewed`, `story_viewers`, `create_reel`; Bucket `reels` (50 MB, nur Video), Größen-/Typ-Limits für Bild-Buckets |
