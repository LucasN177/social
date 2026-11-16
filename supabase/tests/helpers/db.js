// Gemeinsame Hilfsfunktionen: PGlite mit Supabase-Nachbildung starten, setup.sql ausführen,
// als bestimmter User agieren.
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { randomUUID } from 'node:crypto';
import assert from 'node:assert/strict';
import { PGlite } from '@electric-sql/pglite';
import { pg_trgm } from '@electric-sql/pglite/contrib/pg_trgm';
import { pgcrypto } from '@electric-sql/pglite/contrib/pgcrypto';

const here = dirname(fileURLToPath(import.meta.url));
export const BOOTSTRAP_PATH = join(here, '..', 'bootstrap.sql');
export const SETUP_PATH = join(here, '..', '..', 'setup.sql');

/** setup.sql wird bei jedem Lauf frisch gelesen – getestet wird immer die aktuelle Datei. */
export const readSetupSql = () => readFileSync(SETUP_PATH, 'utf8');

/** Neue In-Memory-Datenbank mit Supabase-Nachbildung (ohne setup.sql). */
export async function createBareDb() {
  const db = await PGlite.create({ extensions: { pg_trgm, pgcrypto } });
  await db.exec(readFileSync(BOOTSTRAP_PATH, 'utf8'));
  return db;
}

/** Führt setup.sql aus; bei einem Fehler wird die offene Transaktion zurückgerollt. */
export async function runSetup(db) {
  await db.exec('reset role');
  try {
    await db.exec(readSetupSql());
  } catch (err) {
    await db.exec('rollback').catch(() => {});
    throw err;
  }
}

/** Neue Datenbank inkl. setup.sql. */
export async function createDb() {
  const db = await createBareDb();
  await runSetup(db);
  return db;
}

/** Zurück zum Superuser (entspricht dem Supabase-SQL-Editor). */
export async function asSuperuser(db) {
  await db.exec(`reset role; select set_config('request.jwt.claim.sub', '', false);`);
}

/** Als eingeloggter User agieren (Rolle authenticated + JWT-Claim sub). */
export async function asUser(db, userId) {
  await db.exec('reset role');
  await db.query(`select set_config('request.jwt.claim.sub', $1, false)`, [userId]);
  await db.exec('set role authenticated');
}

/** Als nicht eingeloggter Besucher agieren. */
export async function asAnon(db) {
  await db.exec(`reset role; select set_config('request.jwt.claim.sub', '', false); set role anon;`);
}

/** Legt einen Auth-User an (Trigger aus 001 erzeugt public.users) und liefert die ID. */
export async function createUser(db, username, { vorname = '', nachname = '' } = {}) {
  const id = randomUUID();
  await asSuperuser(db);
  await db.query(
    `insert into auth.users (id, email, raw_user_meta_data) values ($1, $2, $3)`,
    [id, `${username}@example.com`, JSON.stringify({ username, vorname, nachname, display_name: username })],
  );
  return id;
}

/** Erwartet, dass die Abfrage mit einer zum Muster passenden Fehlermeldung scheitert. */
export async function expectError(db, sql, params, pattern) {
  await assert.rejects(db.query(sql, params), (err) => {
    assert.match(err.message, pattern);
    return true;
  });
}

/** Alle Zeilen. */
export async function rows(db, sql, params = []) {
  return (await db.query(sql, params)).rows;
}

/** Erste Spalte der ersten Zeile (undefined ohne Zeilen). */
export async function scalar(db, sql, params = []) {
  const res = await db.query(sql, params);
  if (res.rows.length === 0) return undefined;
  return Object.values(res.rows[0])[0];
}
