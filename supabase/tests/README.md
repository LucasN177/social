# SQL-Tests (PGlite)

Automatische Tests für [`../setup.sql`](../setup.sql). Sie laufen ohne Supabase-Projekt und ohne Docker:
[PGlite](https://pglite.dev) ist ein echtes Postgres, kompiliert nach WebAssembly, und läuft direkt in Node.

## Ausführen

```bash
cd supabase/tests
npm ci
npm test
```

Voraussetzung: Node.js 20 oder neuer. Testframework ist Node's eingebautes `node:test` mit `node:assert`.

`setup.sql` wird bei jedem Lauf frisch von der Platte gelesen – getestet wird also immer die aktuell
erzeugte Datei. Nach Änderungen an `supabase/sql/*.sql` deshalb zuerst `build-setup.ps1` ausführen.

## Aufbau

| Datei | Inhalt |
|---|---|
| `bootstrap.sql` | Nachbildung der Supabase-Umgebung, läuft vor `setup.sql` |
| `helpers/db.js` | Datenbank starten, `setup.sql` ausführen, als User agieren (`asUser`, `asAnon`, `asSuperuser`), User anlegen (`createUser`) |
| `setup.test.js` | `setup.sql` läuft durch (auch ohne `pg_cron`/`pg_net`), zweiter Lauf (Idempotenz) |
| `posts.test.js` | `update_post`, Spaltenrechte auf `posts`, `edited_at` in `feed_posts`/`home_feed`, Post- und Kommentar-Likes |
| `stories.test.js` | `story_feed`, `media_type`, `reply_to_story`, Story-Bezug in `messages` nur per RPC |
| `messages.test.js` | 1:1- und Gruppenchats, Bearbeiten/Löschen, Bildnachrichten, `unread_count`, Bucket `chat-images` |
| `mentions.test.js` | `@username` in Posts/Kommentaren → Benachrichtigung `mention` |

Jede Testdatei startet eine eigene In-Memory-Datenbank; Node führt die Dateien parallel aus.

Tests für bekannte Fehler in den SQL-Skripten sind mit `{ todo: 'BUG: …' }` markiert: Sie laufen mit,
zeigen den Fehler als `⚠ … # BUG` an, lassen `npm test` aber nicht scheitern. Ist der Fehler behoben,
den `todo`-Eintrag entfernen.

## Was nachgebildet wird (`bootstrap.sql`)

- Rollen `anon`, `authenticated`, `service_role` (ohne Login) und Schema-Rechte wie bei Supabase.
- Standardrechte: `alter default privileges … grant all` auf Tabellen, Funktionen und Sequenzen in `public`
  für alle drei Rollen. Was zählt, sind damit die `revoke`/`grant` der Skripte selbst (z. B. 012, 018, 020).
- Suchpfad `"$user", public, extensions`.
- Schema `extensions` mit vorinstalliertem `pgcrypto`; `pg_trgm` legt `000_extensions.sql` selbst an.
- Schema `auth`: Tabelle `auth.users (id, email, raw_user_meta_data, …)` und `auth.uid()` /
  `auth.role()` aus den Einstellungen `request.jwt.claim.sub` bzw. `request.jwt.claim.role`.
- Schema `storage`: `storage.buckets`, `storage.objects` (RLS aktiv) und `storage.foldername()`.
- Publication `supabase_realtime`.

Als User agieren: `set role authenticated` + `set_config('request.jwt.claim.sub', '<uuid>', false)`
(siehe `asUser`). User werden über `insert into auth.users` angelegt, der Trigger aus `001_users.sql`
erzeugt die Zeile in `public.users`.

## Bekannte Grenzen

- **Kein `pg_cron`, `pg_net`, `vault`**: Der DO-Block in `019` fängt das ab; `invoke_story_cleanup()`
  wird angelegt, aber nicht ausgeführt. Das Aufräumen abgelaufener Stories (Edge Function) wird nicht getestet.
- **Kein PostgREST / keine Storage-API**: RPCs und Tabellen werden direkt per SQL angesprochen.
  Bucket-Limits (`file_size_limit`, `allowed_mime_types`) prüft bei Supabase die Storage-API – hier nicht.
  Storage-Policies werden durch direkte `insert`/`select`/`delete` auf `storage.objects` geprüft.
- **Besitzer der Objekte ist der Superuser `postgres`**: `security definer`-Funktionen laufen damit
  als Superuser. Bei Supabase ist `postgres` kein Superuser, aber Besitzer der Tabellen in `public`
  (umgeht RLS dort also ebenso). `storage.objects` gehört bei Supabase `supabase_storage_admin`.
- **Kein Realtime**: Es wird nur geprüft, welche Tabellen in `supabase_realtime` stehen.
- **Zeit**: `now()` ist wie in echtem Postgres die Startzeit der Transaktion; jede Abfrage der Tests
  läuft in einer eigenen Transaktion (wie jeder Request über PostgREST).
- PGlite ist eine einzelne Verbindung – Nebenläufigkeit (Sperren, parallele Requests) wird nicht getestet.
