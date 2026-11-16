// Posts bearbeiten (002/013/018), Kommentar-Likes und Zähler (003/008/018).
import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { createDb, createUser, asUser, asSuperuser, scalar, rows, expectError } from './helpers/db.js';

let db;
before(async () => { db = await createDb(); });
after(async () => { await db.close(); });

async function createPost(userId, content = 'Hallo Welt') {
  await asUser(db, userId);
  return Number(await scalar(db, `select public.create_post($1)`, [content]));
}

test('Besitzer kann update_post aufrufen, edited_at wird gesetzt', async () => {
  const alice = await createUser(db, 'post_alice');
  const postId = await createPost(alice, 'vorher');

  await asUser(db, alice);
  assert.equal(await scalar(db, `select edited_at from posts where id = $1`, [postId]), null);

  await db.query(`select public.update_post($1, $2, $3, $4)`, [postId, 'nachher', 'Berlin', ['tag']]);
  const [row] = await rows(db, `select content, location, tags, edited_at from posts where id = $1`, [postId]);
  assert.equal(row.content, 'nachher');
  assert.equal(row.location, 'Berlin');
  assert.deepEqual(row.tags, ['tag']);
  assert.ok(row.edited_at instanceof Date);
});

test('Fremder User kann update_post nicht aufrufen, Zeile bleibt unverändert', async () => {
  const alice = await createUser(db, 'post_alice2');
  const bob = await createUser(db, 'post_bob2');
  const postId = await createPost(alice, 'original');

  await asUser(db, bob);
  await expectError(db, `select public.update_post($1, $2)`, [postId, 'gehackt'], /post not found/);

  await asSuperuser(db);
  const [row] = await rows(db, `select content, edited_at from posts where id = $1`, [postId]);
  assert.equal(row.content, 'original');
  assert.equal(row.edited_at, null);

  // Auch direkt per UPDATE: RLS filtert die Zeile weg
  await asUser(db, bob);
  const res = await db.query(`update posts set content = 'gehackt' where id = $1`, [postId]);
  assert.equal(res.affectedRows, 0);
});

test('Zähler sind auch am eigenen Post nicht direkt änderbar (Spaltenrechte aus 018)', async () => {
  const alice = await createUser(db, 'post_alice3');
  const postId = await createPost(alice);

  await asUser(db, alice);
  for (const column of ['likes_count', 'comments_count', 'shares_count', 'user_id', 'media_type']) {
    await expectError(db, `update posts set ${column} = ${column} where id = $1`, [postId], /permission denied/);
  }
  // Erlaubte Spalten gehen weiterhin
  const res = await db.query(`update posts set content = 'direkt' where id = $1`, [postId]);
  assert.equal(res.affectedRows, 1);

  await asSuperuser(db);
  assert.equal(await scalar(db, `select likes_count from posts where id = $1`, [postId]), 0);
});

test('feed_posts und home_feed liefern edited_at', async () => {
  const alice = await createUser(db, 'post_alice4');
  const bob = await createUser(db, 'post_bob4');
  const postId = await createPost(alice, 'Feed');

  await asUser(db, bob);
  await db.query(`insert into follows (follower_id, following_id) values (auth.uid(), $1)`, [alice]);

  await asUser(db, alice);
  await db.query(`select public.update_post($1, 'Feed bearbeitet')`, [postId]);

  await asUser(db, bob);
  const [feed] = await rows(db, `select content, edited_at from feed_posts where id = $1`, [postId]);
  assert.equal(feed.content, 'Feed bearbeitet');
  assert.ok(feed.edited_at instanceof Date);

  const [home] = await rows(db, `select edited_at from home_feed where id = $1`, [postId]);
  assert.ok(home, 'Post eines gefolgten Accounts fehlt im home_feed');
  assert.ok(home.edited_at instanceof Date);
});

test('Kommentar-Likes: Zähler, is_liked nur für den Liker, Unlike zählt herunter', async () => {
  const alice = await createUser(db, 'cl_alice');
  const bob = await createUser(db, 'cl_bob');
  const carol = await createUser(db, 'cl_carol');
  const postId = await createPost(alice);

  await asUser(db, bob);
  const commentId = Number(await scalar(db,
    `insert into comments (post_id, user_id, content) values ($1, auth.uid(), 'Toll!') returning id`, [postId]));

  await asSuperuser(db);
  assert.equal(await scalar(db, `select comments_count from posts where id = $1`, [postId]), 1);

  await asUser(db, carol);
  await db.query(`insert into comment_likes (comment_id, user_id) values ($1, auth.uid())`, [commentId]);
  // Fremde Likes anlegen geht nicht
  await expectError(db, `insert into comment_likes (comment_id, user_id) values ($1, $2)`, [commentId, alice],
    /row-level security/);

  assert.equal(await scalar(db, `select likes_count from comments where id = $1`, [commentId]), 1);
  assert.equal(await scalar(db, `select is_liked from comment_details where id = $1`, [commentId]), true);

  await asUser(db, bob);
  assert.equal(await scalar(db, `select is_liked from comment_details where id = $1`, [commentId]), false);
  assert.equal(await scalar(db, `select likes_count from comment_details where id = $1`, [commentId]), 1);

  // Kommentar-Zähler darf auch der Autor nicht direkt setzen (kein UPDATE-Policy auf comments)
  const res = await db.query(`update comments set likes_count = 100 where id = $1`, [commentId]);
  assert.equal(res.affectedRows, 0);

  await asUser(db, carol);
  await db.query(`delete from comment_likes where comment_id = $1 and user_id = auth.uid()`, [commentId]);
  assert.equal(await scalar(db, `select likes_count from comments where id = $1`, [commentId]), 0);
  assert.equal(await scalar(db, `select is_liked from comment_details where id = $1`, [commentId]), false);
});

test('Post-Likes zählen likes_count hoch und runter', async () => {
  const alice = await createUser(db, 'pl_alice');
  const bob = await createUser(db, 'pl_bob');
  const postId = await createPost(alice);

  await asUser(db, bob);
  await db.query(`insert into likes (post_id, user_id) values ($1, auth.uid())`, [postId]);
  assert.equal(await scalar(db, `select likes_count from feed_posts where id = $1`, [postId]), 1);
  assert.equal(await scalar(db, `select is_liked from feed_posts where id = $1`, [postId]), true);

  await db.query(`delete from likes where post_id = $1 and user_id = auth.uid()`, [postId]);
  assert.equal(await scalar(db, `select likes_count from feed_posts where id = $1`, [postId]), 0);
});
