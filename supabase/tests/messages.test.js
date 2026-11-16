// Direktnachrichten und Gruppen (016/020), Chat-Bilder im privaten Bucket chat-images (020).
import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { createDb, createUser, asUser, asSuperuser, scalar, rows, expectError } from './helpers/db.js';

let db;
let a, b, c, d, e;
let direct; // 1:1-Unterhaltung a <-> b

const sendMessage = async (convId, content, imagePath = null) =>
  Number(await scalar(db,
    `insert into messages (conversation_id, sender_id, content, image_path)
     values ($1, auth.uid(), $2, $3) returning id`, [convId, content, imagePath]));

const listEntry = async (convId) =>
  (await rows(db, `select * from conversation_list where id = $1`, [convId]))[0];

before(async () => {
  db = await createDb();
  a = await createUser(db, 'msg_a', { vorname: 'Anna' });
  b = await createUser(db, 'msg_b', { vorname: 'Ben' });
  c = await createUser(db, 'msg_c');
  d = await createUser(db, 'msg_d');
  e = await createUser(db, 'msg_e');
});
after(async () => { await db.close(); });

test('get_or_create_direct_conversation legt genau eine 1:1-Unterhaltung an', async () => {
  await asUser(db, a);
  direct = Number(await scalar(db, `select public.get_or_create_direct_conversation($1)`, [b]));
  assert.equal(Number(await scalar(db, `select public.get_or_create_direct_conversation($1)`, [b])), direct);

  await asUser(db, b);
  assert.equal(Number(await scalar(db, `select public.get_or_create_direct_conversation($1)`, [a])), direct);

  await expectError(db, `select public.get_or_create_direct_conversation($1)`, [b], /invalid conversation partner/);
  await expectError(db, `select public.get_or_create_direct_conversation($1)`,
    ['00000000-0000-0000-0000-000000000000'], /user not found/);

  // Ohne Nachricht erscheint eine 1:1-Unterhaltung nicht in der Liste
  assert.equal(await listEntry(direct), undefined);
});

test('create_group_conversation braucht mindestens zwei weitere Mitglieder', async () => {
  await asUser(db, a);
  await expectError(db, `select public.create_group_conversation('G', $1)`, [[b]], /at least two other members/);
  // Sich selbst mitzuzählen hilft nicht
  await expectError(db, `select public.create_group_conversation('G', $1)`, [[b, a]], /at least two other members/);
  await expectError(db, `select public.create_group_conversation('G', $1)`,
    [[b, '00000000-0000-0000-0000-000000000000']], /at least two other members/);
  await expectError(db, `select public.create_group_conversation('   ', $1)`, [[b, c]], /1 to 60 characters/);
});

test('Gruppe erscheint ohne Nachrichten in conversation_list mit is_group, title, member_count', async () => {
  await asUser(db, a);
  const group = Number(await scalar(db, `select public.create_group_conversation(' Team ', $1)`, [[b, c]]));

  const entry = await listEntry(group);
  assert.ok(entry, 'Gruppe fehlt in conversation_list');
  assert.equal(entry.is_group, true);
  assert.equal(entry.title, 'Team');
  assert.equal(entry.member_count, 3);
  assert.equal(entry.last_message, '');
  assert.equal(entry.unread_count, 0);

  // Nicht-Mitglieder sehen sie nicht
  await asUser(db, d);
  assert.equal(await listEntry(group), undefined);
});

test('add_conversation_members, rename_conversation und Rechte von Nicht-Mitgliedern', async () => {
  await asUser(db, a);
  const group = Number(await scalar(db, `select public.create_group_conversation('Club', $1)`, [[b, c]]));

  await asUser(db, e);
  await expectError(db, `select public.add_conversation_members($1, $2)`, [group, [e]], /group not found/);
  await expectError(db, `select public.rename_conversation($1, 'Meins')`, [group], /group not found/);

  await asUser(db, b);
  await db.query(`select public.add_conversation_members($1, $2)`, [group, [d, c]]); // c ist schon drin
  await db.query(`select public.rename_conversation($1, 'Neuer Name')`, [group]);
  const entry = await listEntry(group);
  assert.equal(entry.member_count, 4);
  assert.equal(entry.title, 'Neuer Name');

  // 1:1-Unterhaltungen sind keine Gruppen
  await asUser(db, a);
  await expectError(db, `select public.add_conversation_members($1, $2)`, [direct, [c]], /group not found/);
  await expectError(db, `select public.rename_conversation($1, 'x')`, [direct], /group not found/);
  await expectError(db, `select public.leave_conversation($1)`, [direct], /group not found/);
});

test('Gruppe mit zwei Mitgliedern zählt nicht als 1:1-Unterhaltung', async () => {
  await asUser(db, c);
  const group = Number(await scalar(db, `select public.create_group_conversation('Trio', $1)`, [[d, e]]));

  await asUser(db, d);
  await db.query(`select public.leave_conversation($1)`, [group]);

  await asUser(db, c);
  assert.equal((await listEntry(group)).member_count, 2);
  const directCe = Number(await scalar(db, `select public.get_or_create_direct_conversation($1)`, [e]));
  assert.notEqual(directCe, group);
  assert.equal(await scalar(db, `select is_group from conversations where id = $1`, [directCe]), false);
});

test('leave_conversation: verlässt das letzte Mitglied die Gruppe, wird sie gelöscht', async () => {
  await asUser(db, c);
  const group = Number(await scalar(db, `select public.create_group_conversation('Weg', $1)`, [[d, e]]));
  await sendMessage(group, 'Hallo Gruppe');

  for (const user of [c, d]) {
    await asUser(db, user);
    await db.query(`select public.leave_conversation($1)`, [group]);
    assert.equal(await listEntry(group), undefined);
  }
  // Nochmal verlassen geht nicht
  await asUser(db, c);
  await expectError(db, `select public.leave_conversation($1)`, [group], /group not found/);

  await asSuperuser(db);
  assert.equal(await scalar(db, `select count(*)::int from conversations where id = $1`, [group]), 1);

  await asUser(db, e);
  await db.query(`select public.leave_conversation($1)`, [group]);

  await asSuperuser(db);
  assert.equal(await scalar(db, `select count(*)::int from conversations where id = $1`, [group]), 0);
  assert.equal(await scalar(db, `select count(*)::int from messages where conversation_id = $1`, [group]), 0);
});

test('Nicht-Mitglieder können Nachrichten weder lesen noch schreiben', async () => {
  await asUser(db, a);
  await sendMessage(direct, 'Geheim');

  await asUser(db, e);
  assert.equal(await scalar(db, `select count(*)::int from messages where conversation_id = $1`, [direct]), 0);
  assert.equal(await scalar(db, `select count(*)::int from conversations where id = $1`, [direct]), 0);
  assert.equal(await scalar(db,
    `select count(*)::int from conversation_members where conversation_id = $1`, [direct]), 0);
  await expectError(db, `insert into messages (conversation_id, sender_id, content) values ($1, auth.uid(), 'x')`,
    [direct], /row-level security/);

  // Als jemand anderes schreiben geht auch für Mitglieder nicht
  await asUser(db, b);
  await expectError(db, `insert into messages (conversation_id, sender_id, content) values ($1, $2, 'x')`,
    [direct, a], /row-level security/);
});

test('edit_message nur durch den Absender, setzt edited_at und erhöht updated_at', async () => {
  await asUser(db, a);
  const msgId = await sendMessage(direct, 'Tippfehlr');
  const before = await scalar(db, `select updated_at from messages where id = $1`, [msgId]);

  await asUser(db, b);
  await expectError(db, `select public.edit_message($1, 'gehackt')`, [msgId], /message not found/);
  const res = await db.query(`update messages set content = 'gehackt' where id = $1`, [msgId]);
  assert.equal(res.affectedRows, 0);

  await asUser(db, a);
  await db.query(`select public.edit_message($1, '  Tippfehler  ')`, [msgId]);
  const [row] = await rows(db, `select content, edited_at, updated_at from messages where id = $1`, [msgId]);
  assert.equal(row.content, 'Tippfehler');
  assert.ok(row.edited_at instanceof Date);
  assert.ok(row.updated_at > before, 'updated_at wurde nicht erhöht');

  // Leer ohne Bild geht nicht
  await expectError(db, `select public.edit_message($1, '   ')`, [msgId], /message not found/);
  // Absender/Unterhaltung lassen sich nicht direkt ändern (Spaltenrechte)
  await expectError(db, `update messages set sender_id = $2 where id = $1`, [msgId, b], /permission denied/);
  await expectError(db, `update messages set conversation_id = conversation_id where id = $1`, [msgId],
    /permission denied/);
});

test('Bildnachricht ohne Text nur mit passendem Pfad <conversation_id>/<sender_id>/…', async () => {
  await asUser(db, a);
  const ok = await sendMessage(direct, '', `${direct}/${a}/foto.jpg`);
  assert.ok(ok > 0);

  await expectError(db, `insert into messages (conversation_id, sender_id, content, image_path)
     values ($1, auth.uid(), '', $2)`, [direct, `${direct}/${b}/foto.jpg`], /row-level security/);
  await expectError(db, `insert into messages (conversation_id, sender_id, content, image_path)
     values ($1, auth.uid(), '', $2)`, [direct, `999999/${a}/foto.jpg`], /row-level security/);
  // Weder Text noch Bild
  await expectError(db, `insert into messages (conversation_id, sender_id, content) values ($1, auth.uid(), '')`,
    [direct], /messages_content_valid/);
});

test('Direktes UPDATE von image_path auf den Ordner eines anderen Mitglieds wird abgelehnt', async () => {
  await asUser(db, a);
  const msgId = await sendMessage(direct, 'Text');
  await expectError(db, `update messages set image_path = $2 where id = $1`,
    [msgId, `${direct}/${b}/fremd.jpg`], /row-level security/);
});

test('Direktes UPDATE von image_path auf eine andere Unterhaltung wird abgelehnt', async () => {
  await asUser(db, a);
  const msgId = await sendMessage(direct, 'Text');
  await expectError(db, `update messages set image_path = $2 where id = $1`,
    [msgId, `999999/${a}/x.jpg`], /row-level security/);
});

test('delete_message löscht weich und liefert den alten Bildpfad', async () => {
  await asUser(db, a);
  const path = `${direct}/${a}/loeschen.jpg`;
  const msgId = await sendMessage(direct, 'Mit Bild', path);

  await asUser(db, b);
  await expectError(db, `select public.delete_message($1)`, [msgId], /message not found/);

  await asUser(db, a);
  assert.equal(await scalar(db, `select public.delete_message($1)`, [msgId]), path);
  const [row] = await rows(db, `select content, image_path, deleted_at from messages where id = $1`, [msgId]);
  assert.equal(row.content, '');
  assert.equal(row.image_path, null);
  assert.ok(row.deleted_at instanceof Date);

  // Zweimal löschen geht nicht, gelöschte Nachrichten lassen sich nicht bearbeiten
  await expectError(db, `select public.delete_message($1)`, [msgId], /message not found/);
  await expectError(db, `select public.edit_message($1, 'wieder da')`, [msgId], /message not found/);

  const entry = await listEntry(direct);
  assert.equal(entry.last_is_deleted, true);
  assert.equal(entry.last_message, '');
});

test('unread_count ignoriert gelöschte Nachrichten; mark_conversation_read setzt zurück', async () => {
  const x = await createUser(db, 'msg_x', { vorname: 'Xaver' });
  const y = await createUser(db, 'msg_y');

  await asUser(db, x);
  const conv = Number(await scalar(db, `select public.get_or_create_direct_conversation($1)`, [y]));
  await sendMessage(conv, 'eins');
  const zwei = await sendMessage(conv, 'zwei');
  await sendMessage(conv, '', `${conv}/${x}/bild.png`);
  await db.query(`select public.delete_message($1)`, [zwei]);

  const own = await listEntry(conv);
  assert.equal(own.unread_count, 0); // eigene Nachrichten zählen nicht

  await asUser(db, y);
  const entry = await listEntry(conv);
  assert.equal(entry.unread_count, 2);
  assert.equal(entry.other_user_id, x);
  assert.equal(entry.last_is_image, true);
  assert.equal(entry.last_sender_name, 'Xaver');
  assert.equal(await scalar(db, `select public.unread_message_count()`), 2);

  await db.query(`select public.mark_conversation_read($1)`, [conv]);
  assert.equal((await listEntry(conv)).unread_count, 0);
});

test('conversation_member_profiles liefert die Mitglieder nur für Mitglieder', async () => {
  await asUser(db, a);
  const group = Number(await scalar(db, `select public.create_group_conversation('Profile', $1)`, [[b, c]]));
  const names = (await rows(db, `select username from public.conversation_member_profiles($1)`, [group]))
    .map((r) => r.username);
  assert.deepEqual(names, ['msg_a', 'msg_b', 'msg_c']);

  await asUser(db, e);
  assert.equal((await rows(db, `select * from public.conversation_member_profiles($1)`, [group])).length, 0);
});

// ---------------------------------------------------------------------
// Storage: chat-images
// ---------------------------------------------------------------------

const insertObject = (name) =>
  db.query(`insert into storage.objects (bucket_id, name) values ('chat-images', $1)`, [name]);

test('chat-images: Mitglied darf in <conv>/<eigene id>/ hochladen, sonst niemand', async () => {
  await asUser(db, a);
  await insertObject(`${direct}/${a}/x.jpg`);

  // In den Ordner eines anderen Users
  await assert.rejects(insertObject(`${direct}/${b}/x.jpg`), /row-level security/);
  // Ungültiger Ordnername
  await assert.rejects(insertObject(`abc/${a}/x.jpg`), /row-level security/);

  // Nicht-Mitglied
  await asUser(db, e);
  await assert.rejects(insertObject(`${direct}/${e}/x.jpg`), /row-level security/);

  // Lesen: Mitglied b ja, Nicht-Mitglied e nein
  const count = `select count(*)::int from storage.objects where bucket_id = 'chat-images' and name = $1`;
  assert.equal(await scalar(db, count, [`${direct}/${a}/x.jpg`]), 0);
  await asUser(db, b);
  assert.equal(await scalar(db, count, [`${direct}/${a}/x.jpg`]), 1);

  // Löschen: nur eigene Dateien
  const del = await db.query(`delete from storage.objects where bucket_id = 'chat-images' and name = $1`,
    [`${direct}/${a}/x.jpg`]);
  assert.equal(del.affectedRows, 0);
  await asUser(db, a);
  const delOwn = await db.query(`delete from storage.objects where bucket_id = 'chat-images' and name = $1`,
    [`${direct}/${a}/x.jpg`]);
  assert.equal(delOwn.affectedRows, 1);
});

test('can_access_chat_file liefert false für ungültige Ordnernamen ohne Fehler', async () => {
  await asUser(db, a);
  for (const name of ['abc/x.jpg', '', 'x', '12a/foo', '-1/foo', '1.5/foo', '1234567890123456789/foo',
    '99999999999999999999999/foo', '/foo']) {
    assert.equal(await scalar(db, `select public.can_access_chat_file($1)`, [name]), false, name);
  }
  assert.equal(await scalar(db, `select public.can_access_chat_file($1)`, [`${direct}/${a}/x.jpg`]), true);
  assert.equal(await scalar(db, `select public.can_access_chat_file($1)`, [`${direct}`]), true);
});
