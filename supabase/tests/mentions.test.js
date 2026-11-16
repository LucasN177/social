// Erwähnungen (021): @username in Posts und Kommentaren erzeugt Benachrichtigungen vom Typ mention.
import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { createDb, createUser, asUser, asSuperuser, scalar, rows } from './helpers/db.js';

let db;
let alice, bob, carol, dave;

before(async () => {
  db = await createDb();
  alice = await createUser(db, 'alice');
  bob = await createUser(db, 'bob');
  carol = await createUser(db, 'carol');
  dave = await createUser(db, 'dave');
});
after(async () => { await db.close(); });

async function mentionCount(userId) {
  await asSuperuser(db);
  return scalar(db, `select count(*)::int from notifications where user_id = $1 and type = 'mention'`, [userId]);
}

async function createPost(userId, content) {
  await asUser(db, userId);
  return Number(await scalar(db, `select public.create_post($1)`, [content]));
}

test('Post mit @bob erzeugt genau eine mention-Benachrichtigung für bob, keine für den Autor', async () => {
  const postId = await createPost(alice, 'Hallo @bob und @bob, ich bin @alice');
  assert.equal(await mentionCount(bob), 1);
  assert.equal(await mentionCount(alice), 0);

  const [n] = await rows(db, `select actor_id, post_id, message from notifications
    where user_id = $1 and type = 'mention'`, [bob]);
  assert.equal(n.actor_id, alice);
  assert.equal(Number(n.post_id), postId);
  assert.equal(n.message, 'mentioned you in a post');

  // bob sieht sie in notification_feed
  await asUser(db, bob);
  assert.equal(await scalar(db, `select count(*)::int from notification_feed where type = 'mention'`), 1);
});

test('Bearbeiten benachrichtigt nur neu Erwähnte', async () => {
  const before = { bob: await mentionCount(bob), carol: await mentionCount(carol) };
  const postId = await createPost(alice, 'Mit @bob');
  assert.equal(await mentionCount(bob), before.bob + 1);

  await asUser(db, alice);
  await db.query(`select public.update_post($1, 'Mit @bob und @carol')`, [postId]);
  assert.equal(await mentionCount(bob), before.bob + 1);
  assert.equal(await mentionCount(carol), before.carol + 1);

  // Nur Ort ändern (content gleich) erzeugt nichts
  await asUser(db, alice);
  await db.query(`select public.update_post($1, 'Mit @bob und @carol', 'Köln')`, [postId]);
  assert.equal(await mentionCount(bob), before.bob + 1);
  assert.equal(await mentionCount(carol), before.carol + 1);
});

test('Kommentar mit Erwähnung des Post-Autors: nur comment-, keine mention-Benachrichtigung', async () => {
  const postId = await createPost(alice, 'Kommentiert mich');
  const aliceBefore = await mentionCount(alice);
  const carolBefore = await mentionCount(carol);

  await asUser(db, dave);
  await db.query(`insert into comments (post_id, user_id, content) values ($1, auth.uid(), '@alice schau, @carol auch')`,
    [postId]);

  assert.equal(await mentionCount(alice), aliceBefore);
  assert.equal(await mentionCount(carol), carolBefore + 1);
  assert.equal(await scalar(db,
    `select count(*)::int from notifications where user_id = $1 and type = 'comment' and post_id = $2`,
    [alice, postId]), 1);
  assert.equal(await scalar(db,
    `select message from notifications where user_id = $1 and type = 'mention' order by id desc limit 1`,
    [carol]), 'mentioned you in a comment');
});

test('Punkt am Ende (@bob.) erwähnt bob', async () => {
  const before = await mentionCount(bob);
  await createPost(dave, 'Danke @bob.');
  assert.equal(await mentionCount(bob), before + 1);
});

test('E-Mail-Adresse x@bob.com erwähnt bob nicht', async () => {
  const before = await mentionCount(bob);
  await createPost(dave, 'Schreib an x@bob.com oder a.b@bob');
  assert.equal(await mentionCount(bob), before);
});

test('Erwähnung ist unabhängig von Groß-/Kleinschreibung, unbekannte Namen werden ignoriert', async () => {
  const before = await mentionCount(carol);
  await createPost(dave, '(@CAROL) und @niemand_hier');
  assert.equal(await mentionCount(carol), before + 1);
});

test('mentioned_user_ids ist für Clients nicht direkt aufrufbar', async () => {
  await asUser(db, alice);
  await assert.rejects(db.query(`select * from public.mentioned_user_ids('@bob', $1)`, [alice]), /permission denied/);
});
