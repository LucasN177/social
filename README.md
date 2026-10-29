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
| Feed mit Posts, Likes, Lesezeichen, Kommentaren | 🚧 (UI vorhanden, noch Mock-Daten) |
| Stories | 🚧 |
| Profil, Folgen, Suche | 📋 geplant |
| Benachrichtigungen (Realtime), Nachrichten, Reels | 📋 geplant |

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

Alle Skripte liegen in [`supabase/sql`](supabase/sql/README.md) und werden **in Nummernreihenfolge** im Supabase SQL-Editor ausgeführt. Sie sind idempotent und können erneut ausgeführt werden.

Neue Datenbankänderungen kommen immer als neues Skript mit der nächsten Nummer dazu – bestehende Skripte werden nicht verändert.

In den Supabase Auth-Einstellungen unter *URL Configuration* die Site-URL (z. B. `https://<user>.github.io/social/`) und `http://localhost:5046` als Redirect-URL eintragen.

## Deployment

Jeder Push auf `main` baut die App über [`.github/workflows/deploy.yml`](.github/workflows/deploy.yml) und veröffentlicht sie auf GitHub Pages unter `/social/`. Der Workflow setzt dafür `<base href="/social/">` und kopiert `index.html` nach `404.html`, damit Deep-Links funktionieren.

### Einmalig: Supabase-Secrets hinterlegen

Der Workflow schreibt `wwwroot/appsettings.json` vor dem Build aus zwei Repository-Secrets. Fehlen sie, bricht der Build mit einer klaren Fehlermeldung ab.

GitHub → Repository → *Settings* → *Secrets and variables* → *Actions* → *New repository secret*:

| Secret | Wert |
|---|---|
| `SUPABASE_URL` | `https://<projekt>.supabase.co` |
| `SUPABASE_ANON_KEY` | der `anon` / `public` Key |

Oder per GitHub CLI (fragt den Wert interaktiv ab):

```bash
gh secret set SUPABASE_URL
```

```bash
gh secret set SUPABASE_ANON_KEY
```
