<p align="center">
  <img src="wwwroot/favicon.svg" width="88" alt="Social Logo" />
</p>

<h1 align="center">Social</h1>

<p align="center">
  Ein Social Network als Progressive Web App – gebaut mit Blazor WebAssembly, MudBlazor und Supabase.<br/>
  <sub>by Lucas Nagelsmann</sub>
</p>

---

## Features

| Bereich | Status |
|---|---|
| Registrierung & Login (Supabase Auth) | ✅ |
| Session bleibt nach Reload erhalten | ✅ |
| Hell-/Dunkelmodus (folgt dem System, Wahl wird gespeichert) | ✅ |
| Vorschläge „Suggestions for you“ (RPC `suggested_users`) | ✅ |
| Beiträge mit bis zu 10 Fotos, Hashtags, Ort erstellen, bearbeiten und löschen | ✅ |
| Feed mit Infinite Scroll, Likes, Lesezeichen, Kommentare mit Likes | ✅ |
| Stories (24 h, Fotos und Videos, mehrere auf einmal) mit Viewer, Gesehen-Status, Aufrufen und Antworten per Nachricht | ✅ |
| Abgelaufene Stories inkl. Dateien automatisch löschen (Edge Function + pg_cron) | ✅ (einmalige Einrichtung, siehe unten) |
| Reels: Videos hochladen, Snap-Scroll mit Autoplay | ✅ |
| Profil ansehen & bearbeiten, Profilbild hochladen | ✅ |
| Folgen / Entfolgen, Follower-Listen, Feed „Following“ / „Explore“ | ✅ |
| Suche nach Accounts und Hashtags, Hashtag-Seiten | ✅ |
| Benachrichtigungen mit Badge (Like, Kommentar, Follow) | ✅ (Polling, Realtime geplant) |
| Direktnachrichten 1:1 mit Ungelesen-Zähler | ✅ (Polling, Realtime geplant) |
| Installierbar als PWA mit Offline-Cache | ✅ |

Der vollständige Fahrplan steht im [Implementationsplan](docs/IMPLEMENTATIONSPLAN.md).

## Tech-Stack

- **Frontend:** [Blazor WebAssembly](https://learn.microsoft.com/aspnet/core/blazor/) (.NET 10), [MudBlazor](https://mudblazor.com/) 9
- **Backend:** [Supabase](https://supabase.com/) – Postgres mit Row Level Security, Auth, Storage, Realtime
- **Hosting:** GitHub Pages via GitHub Actions, installierbar als PWA

## Projektstruktur

```
Core/             Interfaces, Datenbank-Modelle, Response-Typen
Infrastructure/   Repositories – einziger Ort mit direktem Supabase-Zugriff
Services/         Geschäftslogik, App-State, Theme, Auth-State
Layout/           MainLayout, Theme
Pages/            Seiten und Komponenten
supabase/sql/     Alle SQL-Skripte für die Datenbank (nummeriert)
docs/             Implementationsplan
wwwroot/          index.html (Ladebildschirm), CSS, Icons, Manifest
```

## Lokal starten

Voraussetzung: [.NET 10 SDK](https://dotnet.microsoft.com/download).

1. Supabase-Zugangsdaten in **`wwwroot/appsettings.Development.json`** eintragen (Supabase Dashboard → *Project Settings* → *API*). Die Datei ist in `.gitignore`, die Keys landen also nicht im Repo; `appsettings.json` behält die Platzhalter:

   ```json
   {
     "Supabase": {
       "Url": "https://<projekt>.supabase.co",
       "AnonKey": "<anon-public-key>"
     }
   }
   ```

   > Der Anon-Key ist öffentlich und landet ohnehin im Browser. Die Sicherheit kommt ausschließlich aus den RLS-Policies in `supabase/sql`. Den `service_role`-Key niemals hier eintragen.

2. App starten:

   ```bash
   dotnet run --launch-profile http
   ```

3. Im Browser öffnen: <http://localhost:5046>

## Datenbank einrichten

Den kompletten Inhalt von [`supabase/setup.sql`](supabase/setup.sql) im Supabase **SQL Editor** einfügen und ausführen – fertig. Das Skript läuft in einer Transaktion, ist idempotent und kann nach jedem Update einfach erneut ausgeführt werden.

`setup.sql` wird aus den nummerierten Skripten in [`supabase/sql`](supabase/sql/README.md) erzeugt (`supabase/build-setup.ps1`). Neue Datenbankänderungen kommen immer als neues nummeriertes Skript dazu; danach `setup.sql` neu erzeugen.

### Abgelaufene Stories automatisch löschen

Stories verschwinden nach 24 h aus der App. Damit auch Zeilen und Dateien im Bucket `stories` gelöscht werden, ruft `pg_cron` stündlich die Edge Function [`cleanup-expired-stories`](supabase/functions/cleanup-expired-stories/index.ts) auf (eingerichtet von `019_stories_media_replies_cleanup.sql`). Einmalig nötig:

1. Edge Function deployen – entweder automatisch über [`.github/workflows/supabase-functions.yml`](.github/workflows/supabase-functions.yml) (Repository-Secrets `SUPABASE_ACCESS_TOKEN` und `SUPABASE_PROJECT_REF` setzen, dann den Workflow einmal manuell starten) oder lokal mit der Supabase CLI:

   ```bash
   supabase functions deploy cleanup-expired-stories --project-ref <projekt-ref> --no-verify-jwt
   ```

2. Projekt-URL im Vault hinterlegen (SQL Editor):

   ```sql
   select vault.create_secret('https://<projekt-ref>.supabase.co', 'project_url');
   ```

Ohne Secret überspringt der Job das Aufräumen. Die Funktion löscht ausschließlich bereits abgelaufene Stories, deshalb läuft sie ohne JWT-Prüfung.

In den Supabase Auth-Einstellungen unter *URL Configuration* die Site-URL (z. B. `https://<user>.github.io/social/`) und `http://localhost:5046` als Redirect-URL eintragen.

## Deployment

Jeder Push auf `main` baut die App über [`.github/workflows/deploy.yml`](.github/workflows/deploy.yml) und veröffentlicht sie auf GitHub Pages unter `/social/`. Der Workflow setzt dafür `<base href="/social/">` und kopiert `index.html` nach `404.html`, damit Deep-Links funktionieren.

### Einmalig: Supabase-Secrets hinterlegen

Der Workflow schreibt `wwwroot/appsettings.json` vor dem Build aus zwei Repository-Secrets. Fehlen sie, bricht der Build mit einer klaren Fehlermeldung ab.

GitHub → Repository → *Settings* → *Secrets and variables* → *Actions* → *New repository secret*:

| Secret | Wert |
|---|---|
| `SUPABASE_URL` | `https://<projekt>.supabase.co` |
| `SUPABASE_KEY` | der `anon` / `public` Key |

Oder per GitHub CLI (fragt den Wert interaktiv ab):

```bash
gh secret set SUPABASE_URL
```

```bash
gh secret set SUPABASE_KEY
```
