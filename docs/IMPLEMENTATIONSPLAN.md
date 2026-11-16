# Implementationsplan – Social (Blazor WASM · MudBlazor · Supabase)

Stand: 29.10.2026

## 1. Ausgangslage

| Bereich | Status |
|---|---|
| Hosting | Blazor WebAssembly (.NET 10), Deployment via GitHub Actions auf GitHub Pages (`/social/`) |
| UI | MudBlazor 9.3, Layout mit oberer AppBar + Bottom-Navigation (Home, Reels, Messages, Search, Profile) |
| Backend | Supabase-Client (`Supabase` 1.1.1) als Singleton in `Program.cs`, Konfiguration über `wwwroot/appsettings.json` |
| Auth | `AuthRepository` / `AuthService`: Login, Registrierung, Logout, Session, `users`-Metadaten lesen/schreiben |
| Daten | `PostService`, `NotificationService` liefern **Mock-Daten**; `User`, `Post`, `Comment`, `Story`, `Notification` sind noch keine Supabase-Modelle (außer `User`/`UserDto`) |
| Seiten | `Login` (`/`), `Home` (`/fyp`), `NotFound`; Komponenten `PostCard`, `StoriesComponent` |

### Bekannte Probleme

Behoben in Phase 0:

- ~~`IUserService` nicht registriert~~ → `UserService` + `UserRepository` registriert.
- ~~Namespaces uneinheitlich~~ → `Social.Core.Models.Database` / `.Enums`, alle Service-Interfaces in `Social.Core.Interfaces.Services`, zentrale `_Imports.razor`.
- ~~Drei Theme-Quellen, Dark-Mode-Toggle wirkungslos~~ → nur noch `ThemeService`, Wahl in `localStorage`.
- ~~`Logout()` nicht abgewartet~~, ~~Session nicht persistiert~~, ~~Login mit Bootstrap-Markup~~.
- ~~Absolute Links (`/create`, `/fyp`)~~ → relative Links, damit die App unter `/social/` auf GitHub Pages funktioniert.

Offen:

1. **Supabase-Realtime funktioniert im Browser nicht** mit Paket `Supabase` 1.1.1: Der Realtime-Client setzt WebSocket-Header, was in WebAssembly `PlatformNotSupported` wirft. `AutoConnectRealtime` ist deshalb aus. Die App nutzt **Polling** (`ActivityService`, Chat) – das ist eine bewusste Entscheidung (siehe Phase 5), kein offener Punkt mehr.
2. **`SupabaseOptions.SessionHandler` wird in 1.1.1 ignoriert** → Persistenz wird in `Program.cs` explizit per `Auth.SetPersistence` gesetzt und per `LoadSession()` geladen.
3. ~~Deployment nutzt Platzhalter aus `appsettings.json`~~ → der Workflow schreibt die Datei aus den Secrets `SUPABASE_URL` / `SUPABASE_KEY` (siehe README).
   ~~Offline-Cache auf GitHub Pages defekt~~ → Service Worker leitet die Basis aus seinem Scope ab, der Workflow setzt die base href vor dem Publish.
4. ~~`ISocialNetworkService` mit `int`-IDs~~ → entfernt; Posts/Kommentare nutzen `long`.
5. ~~`User` und `UserDto` mappen beide die Tabelle `users`~~ → zusammengeführt.
6. ~~Der Feed zeigt alle Posts~~ → Home hat jetzt „Following“ (View `home_feed`) und „Explore“.
7. ~~`client.Rpc<List<User>>` ignoriert `[Column]`-Namen~~ → `User` hat zusätzlich `[JsonProperty]`; bei weiteren RPC-Modellen ebenso vorgehen.

## 2. Zielarchitektur

```
Pages / Components (MudBlazor)
        │  inject
        ▼
Services (Social.Services)          ← Geschäftslogik, Caching, State
        │
        ▼
Repositories (Social.Infrastructure) ← nur Supabase-Zugriff (Postgrest, Storage, Realtime)
        │
        ▼
Supabase (Postgres + RLS, Auth, Storage, Realtime)
```

- **Core**: Interfaces, Datenbank-Modelle (`BaseModel` mit `[Table]`/`[Column]`), DTOs, `IResponse`.
- **Infrastructure**: je Aggregat ein Repository (`PostRepository`, `CommentRepository`, `FollowRepository`, `StoryRepository`, `NotificationRepository`, `StorageRepository`).
- **Services**: kapseln Repositories, mappen DB-Modelle → UI-Modelle (`Core/Mapping`).
- **Datenbank**: alle Skripte versioniert unter [`supabase/sql`](../supabase/sql), Sicherheit ausschließlich über **Row Level Security**.

## 3. Phasen

### Phase 0 – Grundlagen & Aufräumen
- [x] Moderner Ladebildschirm in `wwwroot/index.html` („by Lucas Nagelsmann“)
- [x] Ordner `supabase/sql` mit nummerierten SQL-Skripten
- [x] Favicon (SVG + PNG), PWA-Icons und Manifest im Markendesign
- [x] Namespaces vereinheitlichen, fehlende DI-Registrierungen ergänzen
- [x] Ein Theme (`ThemeService`) als Single Source of Truth, Dark-Mode-Toggle darüber, Wahl in `localStorage` speichern
- [x] Session-Persistenz (`LocalStorageSessionHandler`, synchrones JS-Interop)
- [x] `SupabaseAuthStateProvider`, `<AuthorizeRouteView>` + Redirect auf `/login?returnUrl=…`
- [x] Logout im AppBar-Menü (funktioniert auch ohne Serververbindung)

### Phase 1 – Auth & Profil
- [x] Login/Registrierung als MudBlazor-Seite (`/login`, eigenes `AuthLayout`, Tabs, Validierung, verständliche Fehlermeldungen, Enter zum Absenden)
- [x] Registrierung übergibt Username, Vor-/Nachname als User-Metadaten → Trigger `handle_new_user` legt die `users`-Zeile an (funktioniert auch mit E-Mail-Bestätigung)
- [x] Profilseite `/profile` und `/u/{username}`: Avatar (Initialen als Fallback), Bio, Zähler, Tabs „Posts“/„Saved“ (leer bis Phase 2), Profil-Link kopieren
- [x] Profil bearbeiten (`EditProfileDialog`): Name, Username (Prüfung auf Duplikat), Bio (max. 160), Avatar-Upload in Bucket `avatars` (im Browser auf 512 px verkleinert)
- [x] Hinweis „Complete profile“ für Accounts ohne Username
- [x] `User` und `UserDto` zusammengeführt; Profilzugriffe nur noch über `UserRepository`
- [x] `012_users_profile_rules.sql`: Client darf nur Profilfelder ändern (nicht Zähler / `is_verified`), Format-Checks für Username und Bio
- [x] Alte Avatar-Dateien beim Ersetzen aus dem Storage löschen

### Phase 2 – Posts & Feed
- [x] Supabase-Modelle (`FeedPostRecord`, `CommentDetailRecord`, …) + Mapping auf die UI-Modelle `Post` / `Comment` (`Core/Mapping/PostMapper.cs`)
- [x] Beitrag erstellen `/create`: bis zu 10 Fotos (im Browser auf 1080 px verkleinert, sortierbar, erstes = Cover), Text bis 2200 Zeichen, Ort, Hashtags werden automatisch erkannt
- [x] RPC `create_post` legt Post + Bilder atomar an; schlägt sie fehl, werden hochgeladene Bilder wieder gelöscht
- [x] Feed `/` über View `feed_posts` mit Pagination („Load more“, 10 pro Seite); aktualisiert sich nach Erstellen/Löschen
- [x] Like (auch per Doppelklick aufs Bild) / Lesezeichen, optimistisch mit Rücksetzen bei Fehler
- [x] Kommentare anzeigen, schreiben (Enter), löschen (eigene bzw. alle unter eigenem Post)
- [x] Beitrag löschen (nur eigene, RLS) inkl. Bilder im Storage; Link kopieren
- [x] Detailseite `/p/{id}`, Profil-Raster für „Posts“ und „Saved“
- [x] Alte Avatar-Datei wird beim Ersetzen gelöscht
- [x] Beitrag bearbeiten (`EditPostDialog`): Text und Ort, Hashtags werden neu erkannt, Hinweis „edited“; RPC `update_post`, Clients dürfen an `posts` nur noch Inhaltsspalten ändern
- [x] Kommentar-Likes: Herz neben jedem Kommentar, optimistisch, Zähler per Trigger (`comment_details.is_liked`)
- [x] Infinite Scroll statt „Load more“ (`InfiniteScroll`-Komponente mit IntersectionObserver) in Home, Profil-/Hashtag-Raster, Benachrichtigungen und Reels

### Phase 3 – Social Graph & Suche
- [x] Folgen/Entfolgen (`FollowButton`, im Profil, in Vorschlägen, in Listen – bleibt überall synchron), Zähler per Trigger
- [x] Follower-/Gefolgte-Listen im Profil (`FollowListDialog`, RPCs `list_followers` / `list_following`)
- [x] Home: „Following“ (eigene + gefolgte Accounts, View `home_feed`) und „Explore“ (alle); leerer Zustand mit Verweis auf Explore/Suche
- [x] Suche `/search`: Accounts (RPC `search_users`, Trigram) und Hashtags (RPC `search_hashtags`), Suchbegriff in der URL
- [x] Hashtag-Seite `/tags/{tag}`; Hashtags in Posts und „Popular“ verlinkt
- [x] Vorschläge „Wem folgen?“ mit funktionierendem Follow-Button
- [x] Alle SQL-Skripte zusätzlich als `supabase/setup.sql` (eine Transaktion, erzeugt per `supabase/build-setup.ps1`, getestet: zweimal hintereinander ausführbar)

### Phase 4 – Stories & Reels
- [x] Stories-Leiste: eigene Story hinzufügen (Bild, im Browser auf max. 1080 × 1920 verkleinert), Ringe für ungesehen/gesehen, Reihenfolge eigene → ungesehene → gesehene
- [x] Story-Viewer: Vollbild, Fortschrittsbalken (5 s pro Story), Tippen links/rechts, Gedrückthalten/Leertaste = Pause, Pfeiltasten/Escape, Gesehen-Status (`mark_story_viewed`)
- [x] Eigene Story: Aufrufzahl + Liste („Viewed by“, `story_viewers`), löschen inkl. Bild
- [x] Story-Feed nur von mir + gefolgten Accounts, abgelaufene Stories ausgeblendet (View `story_feed`)
- [x] Reels `/reels`: Video-Posts (`media_type = 'video'`), vertikaler Snap-Scroll, Autoplay des sichtbaren Videos (IntersectionObserver, `wwwroot/js/media.js`), Ton an/aus, Tippen = Pause, Like (auch Doppeltipp), Kommentare, Link kopieren, Folgen
- [x] Reel erstellen über `/create` (Umschalter Post/Reel bzw. `create?type=reel`): MP4/WebM/MOV bis 50 MB, Vorschau, RPC `create_reel`, Bucket `reels` mit Größen-/Typ-Limit
- [x] Videos auch im Feed (PostCard) und im Profil-Raster (Vorschaubild + Play-Symbol)
- [x] Abgelaufene Stories automatisch löschen: Edge Function `cleanup-expired-stories` (Dateien über die Storage-API, dann Zeilen), stündlich per `pg_cron` + `pg_net`; Projekt-URL im Vault (`project_url`), Deploy per Workflow `supabase-functions.yml` oder CLI
- [x] Mehrere Bilder/Videos pro Story: Mehrfachauswahl (bis 10), Videos bis 50 MB (`stories.media_type`), Viewer spielt Videos mit Ton an/aus und richtet den Fortschritt nach der Videolänge (max. 60 s)
- [x] Story-Antworten: Eingabefeld im Viewer (pausiert beim Tippen), RPC `reply_to_story` schickt eine Direktnachricht mit Story-Bezug; im Chat mit Vorschau bzw. „Story no longer available“

### Phase 5 – Benachrichtigungen & Nachrichten
- [x] Notifications per DB-Trigger (Like, Kommentar, Follow) – seit 007
- [x] Seite `/notifications` (View `notification_feed`): Auslöser, Post-Vorschau bzw. Follow-Button, ungelesene hervorgehoben, beim Öffnen gelesen (`mark_notifications_read`)
- [x] Badges in AppBar (Herz) und unterer Navigation (Nachrichten) über `ActivityService` (Polling alle 30 s, `unread_notification_count` / `unread_message_count`)
- [x] Direktnachrichten: `conversations`, `conversation_members`, `messages` mit RLS nur für Mitglieder; `get_or_create_direct_conversation`
- [x] Chatliste `/messages` (View `conversation_list`, Ungelesen-Zähler) und Chat `/messages/{id}` (Polling alle 3 s, Tagestrenner, Auto-Scroll nur wenn unten)
- [x] „Message“-Button im Profil
- [x] ~~Echtes Realtime statt Polling~~ → **entschieden: Polling bleibt.** Für diese App reicht es (Chat alle 3 s, Badges alle 30 s), es braucht keine WebSocket-Verbindungen und hält das Free-Tier-Kontingent für Realtime (gleichzeitige Verbindungen, Nachrichten pro Monat) frei. Die Publication für `notifications`/`messages` bleibt bestehen, falls es später doch nötig wird.
- [x] Gruppenchats: anlegen („New group“, Name + mind. 2 Personen, max. 32), Leute hinzufügen, umbenennen, Mitglieder ansehen, verlassen; Absendername/-avatar in Gruppen (RPCs `create_group_conversation`, `add_conversation_members`, `rename_conversation`, `leave_conversation`, `conversation_member_profiles`)
- [x] Nachrichten bearbeiten („edited“) und löschen (Platzhalter „Message deleted“, Bild wird mitgelöscht) über RPCs `edit_message` / `delete_message`; Polling über `updated_at`, damit Änderungen auch beim Gegenüber ankommen
- [x] Bilder im Chat: privater Bucket `chat-images` (nur Mitglieder, Anzeige per Signed URL), im Browser auf 1600 px verkleinert, optional mit Text
- [x] Erwähnungen: `@username` in Posts (auch beim Bearbeiten neu hinzugefügte) und Kommentaren erzeugt per Trigger eine Benachrichtigung vom Typ `mention`; sichtbar auf `/notifications` und im Badge beim nächsten Öffnen (kein Push). In Posts, Kommentaren und Reels sind `@username` und `#hashtags` verlinkt (`RichText`)

### Phase 6 – Qualität & Release
- [x] Loading-Skeletons (`MudSkeleton`) und leere Zustände auf den Hauptseiten
- [ ] Fehlerbehandlung über `IResponse` + zentrale Snackbar
- [ ] Unit-Tests für Services (xUnit + Moq), bUnit für Komponenten
- [ ] SQL-Tests (PGlite, Supabase-Umgebung nachgebaut) ins Repo und in CI übernehmen
- [x] Supabase-Keys im Deploy-Workflow aus GitHub Secrets in `appsettings.json` schreiben
- [x] Service Worker / Offline-Cache für `/social/`: Basis aus dem Scope, base href vor dem Publish, neue Versionen übernehmen sofort (`skipWaiting` / `clients.claim`)
- [x] PWA: App-Icons im Markendesign
- [ ] PWA: eigene Offline-Seite

## 4. Datenbank

Zum Einrichten genügt [`supabase/setup.sql`](../supabase/setup.sql) im Supabase SQL-Editor. Es wird aus den nummerierten Skripten in [`supabase/sql`](../supabase/sql) erzeugt (Details: [`supabase/sql/README.md`](../supabase/sql/README.md)).

| Skript | Inhalt |
|---|---|
| `000_extensions.sql` | Extensions, Hilfsfunktion `set_updated_at` |
| `001_users.sql` | `users`-Profil, Trigger bei Registrierung |
| `002_posts.sql` | `posts`, `post_images` |
| `003_comments.sql` | `comments`, `comment_likes` |
| `004_interactions.sql` | `likes`, `bookmarks` |
| `005_follows.sql` | `follows` |
| `006_stories.sql` | `stories`, `story_views` |
| `007_notifications.sql` | `notifications` + Trigger |
| `008_counters.sql` | Zähler-Trigger (Likes, Kommentare, Follower, Posts) |
| `009_views_and_functions.sql` | View `feed_posts`, RPC `suggested_users` |
| `010_storage.sql` | Buckets `avatars`, `post-images`, `stories` + Policies |
| `011_realtime.sql` | Realtime-Publication für `notifications` |
| `012_users_profile_rules.sql` | Spaltenrechte auf `users`, Format-Checks für Username/Bio |
| `013_posts_api.sql` | RPC `create_post`, View `comment_details`, Feed um Vor-/Nachname erweitert |
| `014_social_graph.sql` | View `home_feed`, RPCs `search_hashtags`, `list_followers`, `list_following` |
| `015_stories_reels.sql` | View `story_feed`, RPCs `mark_story_viewed`, `story_viewers`, `create_reel`, Bucket `reels`, Upload-Limits |
| `016_notifications_messages.sql` | View `notification_feed`, Chat-Tabellen + RLS, Views/RPCs für Chatliste, Lesestand und Ungelesen-Zähler |
| `017_users_cleanup.sql` | Bereinigung älterer `users`-Tabellen (Usernames, NULL-Werte), danach Checks validiert |
| `018_post_edit_comment_likes.sql` | `posts.edited_at`, Spaltenrechte auf `posts`, RPC `update_post`, `feed_posts`/`home_feed` um `edited_at`, `comment_details` um `is_liked` erweitert |
| `019_stories_media_replies_cleanup.sql` | `stories.media_type` (Bild/Video), Bucket `stories` bis 50 MB inkl. Video, `messages.story_id`/`story_url` + RPC `reply_to_story`, `invoke_story_cleanup` + stündlicher `pg_cron`-Job |
| `020_chat_groups_edit_images.sql` | Gruppen (`conversations.is_group`/`title`, RPCs zum Anlegen/Hinzufügen/Umbenennen/Verlassen), Nachrichten bearbeiten/löschen (`edit_message`, `delete_message`, `updated_at` als Polling-Cursor), Bilder im privaten Bucket `chat-images`, `conversation_list` um Gruppen/Bild/gelöscht erweitert |
| `021_mentions.sql` | `mentioned_user_ids`, Trigger auf `posts` (Anlegen/Bearbeiten) und `comments` → Benachrichtigung `mention` |

Neue Änderungen werden **nie** in bestehende Skripte geschrieben, sondern als neues Skript mit der nächsten Nummer angelegt.

## 5. Konventionen

- Tabellen/Spalten in `snake_case`, C#-Properties in `PascalCase` mit `[Column("...")]`.
- IDs: `uuid` (User = `auth.users.id`), Inhalte `bigint generated always as identity`.
- Jede Tabelle hat RLS aktiviert; keine Policy = kein Zugriff.
- Services geben `IResponse` / `IResponse<T>` zurück, keine Exceptions an die UI.
- UI-Texte zentral halten, damit später Lokalisierung (de/en) möglich ist.
