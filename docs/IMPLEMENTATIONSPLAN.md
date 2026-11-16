# Implementationsplan – Social (Blazor WASM · MudBlazor · Supabase)

Stand: 16.11.2026 – alle Phasen 0–7 umgesetzt.

## 1. Aktueller Stand

| Bereich | Status |
|---|---|
| Hosting | Blazor WebAssembly (.NET 10), Deployment per GitHub Actions auf GitHub Pages (`/social/`), nur wenn Unit- und SQL-Tests grün sind; installierbar als PWA mit Offline-Cache und eigener Offline-Seite |
| UI | MudBlazor 9.3, obere AppBar (Erstellen, Benachrichtigungen, Dark Mode, Menü) + untere Navigation (Home, Reels, Nachrichten, Suche, Profil); Sprache Deutsch/Englisch |
| Backend | Supabase-Client (`Supabase` 1.1.1) als Singleton, Konfiguration über `wwwroot/appsettings.json` (im Deployment aus GitHub Secrets) |
| Auth | Login/Registrierung (`/login`), Session-Persistenz, `SupabaseAuthStateProvider` + `AuthorizeRouteView` |
| Daten | Alle Inhalte kommen aus Supabase (Tabellen, Views, RPCs); Sicherheit ausschließlich über RLS, Skripte in `supabase/sql` |
| Aktualität | Polling statt Realtime: Badges alle 30 s (`ActivityService`), offener Chat alle 3 s |
| Qualität | `IFeedbackService` für alle Meldungen, `AppErrorBoundary`, Unit-Tests (`tests/Social.Tests`), SQL-Tests (`supabase/tests`) |

### Seiten

| Route | Seite |
|---|---|
| `/login` | Anmelden / Registrieren (`AuthLayout`) |
| `/`, `/fyp` | Home: Stories, Feed „Following“/„Explore“ mit Infinite Scroll, Vorschläge, beliebte Hashtags |
| `/create`, `/create?type=reel` | Beitrag (bis 10 Fotos) oder Reel erstellen |
| `/p/{id}` | Beitrag mit Kommentaren |
| `/reels` | Reels (Snap-Scroll, Autoplay) |
| `/search` | Accounts und Hashtags suchen |
| `/tags/{tag}` | Beiträge zu einem Hashtag |
| `/profile`, `/u/{username}` | Profil mit Beiträgen/Gespeichert, Follower-Listen, Nachricht schreiben |
| `/notifications` | Benachrichtigungen (Like, Kommentar, Follow, Erwähnung) |
| `/messages`, `/messages/{id}` | Chatliste und Chat (1:1 und Gruppen, Bilder, Bearbeiten/Löschen) |
| `/settings` | Profil bearbeiten, Dunkelmodus, Sprache, Abmelden |
| `/not-found` | 404 |

### Technische Hinweise

1. **Supabase-Realtime funktioniert im Browser nicht** mit Paket `Supabase` 1.1.1: Der Realtime-Client setzt WebSocket-Header, was in WebAssembly `PlatformNotSupported` wirft. `AutoConnectRealtime` ist deshalb aus. Die App nutzt bewusst **Polling** (siehe Phase 5).
2. **`SupabaseOptions.SessionHandler` wird in 1.1.1 ignoriert** → Persistenz wird in `Program.cs` explizit per `Auth.SetPersistence` gesetzt und per `LoadSession()` geladen.
3. **`client.Rpc<List<User>>` ignoriert `[Column]`-Namen** → RPC-Modelle brauchen zusätzlich `[JsonProperty]` (siehe `User`, `HashtagResult`).
4. **Base href**: Links sind relativ, der Deploy-Workflow setzt `<base href="/social/">` vor dem Publish; der Service Worker leitet seine Basis aus dem Scope ab.
5. **Sprache**: Die Kultur wird beim Start gesetzt (`LanguageService`); ein Wechsel lädt die App neu, weil Blazor WASM die Kultur nicht für alle Komponenten zur Laufzeit umstellen kann.

<details>
<summary>Ausgangslage vor Phase 0 (29.10.2026)</summary>

Mock-Daten in `PostService`/`NotificationService`, nur `Login`, `Home` und `NotFound`, `User` und `UserDto` mappten beide `users`, nicht registrierter `IUserService`, uneinheitliche Namespaces, drei Theme-Quellen, nicht persistierte Session, absolute Links (unter `/social/` defekt), Platzhalter-Keys im Deployment, defekter Offline-Cache auf GitHub Pages. Alles in Phase 0–6 behoben.

</details>

## 2. Architektur

```
Pages / Components (MudBlazor)
        │  inject
        ▼
Services (Social.Services)          ← Geschäftslogik, Validierung, Caching, State, Meldungen
        │
        ▼
Repositories (Social.Infrastructure) ← nur Supabase-Zugriff (Postgrest, RPCs, Storage)
        │
        ▼
Supabase (Postgres + RLS, Auth, Storage, Edge Functions, pg_cron)
```

- **Core**: Interfaces, Datenbank-Modelle (`BaseModel` mit `[Table]`/`[Column]`), UI-Modelle, Mapping (`Core/Mapping`), `IResponse`.
- **Infrastructure**: `AuthRepository`, `UserRepository`, `PostRepository`, `StoryRepository`, `NotificationRepository` + `MessageRepository` (`MessagingRepositories.cs`), Hilfen `StorageUrl`, `LocalStorageSessionHandler`.
- **Services**: `AuthService`, `UserService`, `PostService`, `StoryService`, `NotificationService`, `MessageService`, `ActivityService` (Badges), `FeedbackService` (Snackbar), `ThemeService`, `LanguageService`.
- **Resources**: `SharedResource` + `SharedResource.de.resx` (deutsche Texte, Schlüssel = englischer Text).
- **Datenbank**: alle Skripte versioniert unter [`supabase/sql`](../supabase/sql), Sicherheit ausschließlich über **Row Level Security**; Edge Function unter [`supabase/functions`](../supabase/functions).

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
- [x] Fehlerbehandlung über `IResponse` + zentrale Snackbar: `IFeedbackService` (alle Meldungen der UI laufen darüber; technische Texte wie Postgrest-JSON oder Exception-Meldungen werden protokolliert und durch verständliche ersetzt, Netzwerkfehler → Offline-Hinweis), `AppErrorBoundary` im Layout statt der Blazor-Fehlerleiste (nur die Seite zeigt „Something went wrong“, zurücksetzen per „Try again“ oder Navigation)
- [x] Unit-Tests für Services (xUnit + Moq), bUnit für Komponenten: `tests/Social.Tests` (PostService, StoryService, MessageService, FeedbackService, Mapping, `RichText`)
- [x] SQL-Tests (PGlite, Supabase-Umgebung nachgebaut) unter `supabase/tests` (siehe README dort): 40 Tests zu Idempotenz, RLS, RPCs, Triggern und Storage-Policies; dabei gefundene Fehler sind mit `022_chat_fixes.sql` bzw. der Präambel in `setup.sql` behoben
- [x] CI: Workflow `tests.yml` (Pull Requests + Voraussetzung für `deploy.yml`) führt Unit- und SQL-Tests aus
- [x] Supabase-Keys im Deploy-Workflow aus GitHub Secrets in `appsettings.json` schreiben
- [x] Service Worker / Offline-Cache für `/social/`: Basis aus dem Scope, base href vor dem Publish, neue Versionen übernehmen sofort (`skipWaiting` / `clients.claim`)
- [x] PWA: App-Icons im Markendesign
- [x] PWA: eigene Offline-Seite `wwwroot/offline.html` (ohne externe Dateien, lädt automatisch neu, sobald wieder online), vom Service Worker ausgeliefert, wenn eine Seite weder im Cache noch im Netz ist; in der App zusätzlich ein Offline-Banner (`OfflineBanner`)

### Phase 7 – Lokalisierung & Doku
- [x] Deutsch/Englisch: `IStringLocalizer<SharedResource>` (in `_Imports.razor` als `L` injiziert), Schlüssel = englischer Text, Übersetzungen in `Resources/SharedResource.de.resx`; statische Helfer nutzen `Loc.L`
- [x] Sprache: gespeicherte Wahl (`localStorage`), sonst Browsersprache; Auswahl unter Einstellungen → Sprache (lädt die App neu); deutsche Datums-/Zahlenformate (`BlazorWebAssemblyLoadAllGlobalizationData`)
- [x] Meldungen der Services bleiben englische Schlüssel und werden von `FeedbackService` bzw. beim Anzeigen (`@L[_errorMessage]`) übersetzt; Texte mit Zahlen aus Konstanten sind feste Schlüssel
- [x] Offline-Seite und Blazor-Fehlerleiste zweisprachig (nach gespeicherter bzw. Browsersprache)
- [x] Test `LocalizationTests`: jeder `L["…"]`-Schlüssel und jede feste Meldung im Code hat eine deutsche Übersetzung, Platzhalter stimmen überein; ungenutztes `NavMenu` (alte deutsche Texte) entfernt
- [x] Einstellungsseite `/settings` (Profil, Dunkelmodus, Sprache, Abmelden) und Doku auf den aktuellen Stand gebracht

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
| `022_chat_fixes.sql` | Korrekturen aus den SQL-Tests: erste Story-Antwort zählt als ungelesen, `messages_update_own` prüft den Absender-Ordner von `image_path` |

`setup.sql` entfernt am Anfang die später erweiterten Views (`build-setup.ps1`), damit es beliebig oft ausführbar bleibt – das prüfen die SQL-Tests.

Neue Änderungen werden **nie** in bestehende Skripte geschrieben, sondern als neues Skript mit der nächsten Nummer angelegt.

## 5. Konventionen

- Tabellen/Spalten in `snake_case`, C#-Properties in `PascalCase` mit `[Column("...")]`.
- IDs: `uuid` (User = `auth.users.id`), Inhalte `bigint generated always as identity`.
- Jede Tabelle hat RLS aktiviert; keine Policy = kein Zugriff.
- Services geben `IResponse` / `IResponse<T>` zurück, keine Exceptions an die UI.
- UI-Texte nie fest auf Deutsch: englischen Text mit `L["…"]` (bzw. `Loc.L` in statischen Helfern) ausgeben und die Übersetzung in `Resources/SharedResource.de.resx` ergänzen. Platzhalter als `{0}`, Mehrzahl als eigener Schlüssel.
