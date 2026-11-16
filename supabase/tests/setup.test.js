// setup.sql läuft in der nachgebildeten Supabase-Umgebung durch – auch ein zweites Mal.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createBareDb, runSetup, scalar, rows } from './helpers/db.js';

test('setup.sql läuft auf einer frischen Datenbank ohne Fehler durch', async () => {
  const db = await createBareDb();
  await runSetup(db);

  // Stichproben: Tabellen aus 001 bis 021 existieren
  for (const table of ['users', 'posts', 'comments', 'comment_likes', 'stories', 'notifications',
    'conversations', 'conversation_members', 'messages']) {
    assert.ok(await scalar(db, `select to_regclass($1)::text`, [`public.${table}`]), `Tabelle ${table} fehlt`);
  }

  // 019: pg_cron/pg_net gibt es in PGlite nicht – der DO-Block fängt das ab,
  // die Funktion für das Aufräumen existiert trotzdem
  assert.equal(await scalar(db, `select count(*)::int from pg_extension where extname in ('pg_cron', 'pg_net')`), 0);
  assert.ok(await scalar(db, `select to_regprocedure('public.invoke_story_cleanup()')::text`));

  // 011/016: Realtime-Publication
  const published = (await rows(db,
    `select tablename from pg_publication_tables where pubname = 'supabase_realtime' order by 1`))
    .map((r) => r.tablename);
  assert.deepEqual(published, ['messages', 'notifications']);

  // 010/015/019/020: Buckets
  const buckets = await rows(db, `select id, public from storage.buckets order by id`);
  assert.deepEqual(buckets.map((b) => b.id), ['avatars', 'chat-images', 'post-images', 'reels', 'stories']);
  assert.equal(buckets.find((b) => b.id === 'chat-images').public, false);

  await db.close();
});

test('setup.sql lässt sich ein zweites Mal ausführen (idempotent)', async () => {
  const db = await createBareDb();
  await runSetup(db);
  try {
    await runSetup(db);
  } catch (err) {
    // Kurze Meldung statt des kompletten Skripts im Fehlerobjekt
    throw new Error(`zweiter Lauf von setup.sql: ${err.message}`);
  } finally {
    await db.close();
  }
});
