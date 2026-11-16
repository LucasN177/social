// Stories (006/015/019): Sichtbarkeit, media_type, Antworten per Direktnachricht.
import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { createDb, createUser, asUser, asSuperuser, scalar, rows, expectError } from './helpers/db.js';

let db;
let alice, bob, carol;
let aliceStory, bobStory, carolStory, aliceExpired;

async function createStory(userId, url) {
  await asUser(db, userId);
  return Number(await scalar(db,
    `insert into stories (user_id, image_url) values (auth.uid(), $1) returning id`, [url]));
}

before(async () => {
  db = await createDb();
  alice = await createUser(db, 'st_alice');
  bob = await createUser(db, 'st_bob');
  carol = await createUser(db, 'st_carol');

  // bob folgt alice, nicht carol
  await asUser(db, bob);
  await db.query(`insert into follows (follower_id, following_id) values (auth.uid(), $1)`, [alice]);

  aliceStory = await createStory(alice, 'https://cdn/alice.jpg');
  bobStory = await createStory(bob, 'https://cdn/bob.jpg');
  carolStory = await createStory(carol, 'https://cdn/carol.jpg');

  await asSuperuser(db);
  aliceExpired = Number(await scalar(db,
    `insert into stories (user_id, image_url, created_at, expires_at)
     values ($1, 'https://cdn/old.jpg', now() - interval '2 days', now() - interval '1 day') returning id`, [alice]));
});
after(async () => { await db.close(); });

test('story_feed zeigt nur eigene und gefolgte aktive Stories', async () => {
  await asUser(db, bob);
  const ids = (await rows(db, `select id from story_feed order by id`)).map((r) => Number(r.id));
  assert.deepEqual(ids, [aliceStory, bobStory].sort((a, b) => a - b));

  await asUser(db, carol);
  const carolIds = (await rows(db, `select id from story_feed`)).map((r) => Number(r.id));
  assert.deepEqual(carolIds, [carolStory]);
});

test('media_type ist standardmäßig image, video ist erlaubt, anderes nicht', async () => {
  await asUser(db, bob);
  assert.equal(await scalar(db, `select media_type from story_feed where id = $1`, [aliceStory]), 'image');

  const videoId = Number(await scalar(db,
    `insert into stories (user_id, image_url, media_type) values (auth.uid(), 'v.mp4', 'video') returning id`));
  assert.equal(await scalar(db, `select media_type from story_feed where id = $1`, [videoId]), 'video');

  await expectError(db, `insert into stories (user_id, image_url, media_type) values (auth.uid(), 'x', 'gif')`, [],
    /stories_media_type_check/);
});

test('reply_to_story auf eine gefolgte, aktive Story legt Direktnachricht mit Story-Bezug an', async () => {
  await asUser(db, bob);
  const convId = Number(await scalar(db, `select public.reply_to_story($1, $2)`, [aliceStory, '  Schön!  ']));

  const members = (await rows(db,
    `select user_id from conversation_members where conversation_id = $1 order by user_id`, [convId]))
    .map((r) => r.user_id);
  assert.deepEqual(members, [alice, bob].sort());
  assert.equal(await scalar(db, `select is_group from conversations where id = $1`, [convId]), false);

  const [msg] = await rows(db,
    `select sender_id, content, story_id, story_url from messages where conversation_id = $1`, [convId]);
  assert.equal(msg.sender_id, bob);
  assert.equal(msg.content, 'Schön!');
  assert.equal(Number(msg.story_id), aliceStory);
  assert.equal(msg.story_url, 'https://cdn/alice.jpg');

  // Zweite Antwort landet in derselben Unterhaltung
  assert.equal(Number(await scalar(db, `select public.reply_to_story($1, 'Nochmal')`, [aliceStory])), convId);
  assert.equal(Number(await scalar(db, `select public.get_or_create_direct_conversation($1)`, [alice])), convId);
});

test('Erste Story-Antwort (neue Unterhaltung) zählt beim Story-Autor als ungelesen', async () => {
  const dora = await createUser(db, 'st_dora');
  const storyId = await createStory(dora, 'https://cdn/dora.jpg');
  await asUser(db, bob);
  await db.query(`insert into follows (follower_id, following_id) values (auth.uid(), $1)`, [dora]);
  const convId = Number(await scalar(db, `select public.reply_to_story($1, 'Hi Dora')`, [storyId]));

  await asUser(db, dora);
  const [entry] = await rows(db, `select unread_count from conversation_list where id = $1`, [convId]);
  assert.equal(entry.unread_count, 1);
});

test('Story-Antwort in bestehender Unterhaltung zählt als ungelesen', async () => {
  const emil = await createUser(db, 'st_emil');
  const storyId = await createStory(emil, 'https://cdn/emil.jpg');
  await asUser(db, bob);
  await db.query(`insert into follows (follower_id, following_id) values (auth.uid(), $1)`, [emil]);
  const convId = Number(await scalar(db, `select public.get_or_create_direct_conversation($1)`, [emil]));
  assert.equal(Number(await scalar(db, `select public.reply_to_story($1, 'Hi Emil')`, [storyId])), convId);

  await asUser(db, emil);
  const [entry] = await rows(db, `select unread_count from conversation_list where id = $1`, [convId]);
  assert.equal(entry.unread_count, 1);
});

test('reply_to_story scheitert bei nicht gefolgter, eigener oder abgelaufener Story', async () => {
  await asUser(db, bob);
  await expectError(db, `select public.reply_to_story($1, 'hi')`, [carolStory], /story not available/);
  await expectError(db, `select public.reply_to_story($1, 'hi')`, [bobStory], /story not available/);
  await expectError(db, `select public.reply_to_story($1, 'hi')`, [aliceExpired], /story not available/);
  await expectError(db, `select public.reply_to_story($1, '   ')`, [aliceStory], /1 to 2000 characters/);
});

test('reply_to_story ist für anon nicht ausführbar', async () => {
  await asSuperuser(db);
  await db.exec('set role anon');
  await expectError(db, `select public.reply_to_story($1, 'hi')`, [aliceStory], /permission denied/);
  await asSuperuser(db);
});

test('Direktes Insert in messages mit story_id/story_url wird per RLS abgelehnt', async () => {
  await asUser(db, bob);
  const convId = Number(await scalar(db, `select public.get_or_create_direct_conversation($1)`, [alice]));

  await expectError(db,
    `insert into messages (conversation_id, sender_id, content, story_id) values ($1, auth.uid(), 'fake', $2)`,
    [convId, carolStory], /row-level security/);
  await expectError(db,
    `insert into messages (conversation_id, sender_id, content, story_url) values ($1, auth.uid(), 'fake', 'x')`,
    [convId], /row-level security/);

  // Ohne Story-Bezug geht es
  await db.query(`insert into messages (conversation_id, sender_id, content) values ($1, auth.uid(), 'ok')`, [convId]);
});

test('Löschen einer Story leert story_id, story_url bleibt', async () => {
  const storyId = await createStory(alice, 'https://cdn/weg.jpg');
  await asUser(db, bob);
  const convId = Number(await scalar(db, `select public.reply_to_story($1, 'Bald weg')`, [storyId]));

  await asUser(db, alice);
  await db.query(`delete from stories where id = $1`, [storyId]);

  const [msg] = await rows(db,
    `select story_id, story_url from messages where conversation_id = $1 and content = 'Bald weg'`, [convId]);
  assert.equal(msg.story_id, null);
  assert.equal(msg.story_url, 'https://cdn/weg.jpg');
});
