-- 020_chat_groups_edit_images.sql
-- Phase 5: Gruppenchats, Nachrichten bearbeiten/löschen, Bilder im Chat.
-- Bewusst ohne Realtime: die App fragt per Polling ab (updated_at als Cursor).

-- ---------------------------------------------------------------------
-- Gruppen
-- ---------------------------------------------------------------------

alter table public.conversations
    add column if not exists is_group   boolean not null default false,
    add column if not exists title      text,
    add column if not exists created_by uuid references public.users (id) on delete set null;

do $$
begin
    if not exists (select 1 from pg_constraint where conname = 'conversations_title_length') then
        alter table public.conversations
            add constraint conversations_title_length check (title is null or char_length(title) between 1 and 60);
    end if;
end;
$$;

-- 1:1-Unterhaltung: Gruppen mit zwei Mitgliedern nicht mitzählen
create or replace function public.get_or_create_direct_conversation(p_other uuid)
returns bigint
language plpgsql
security definer
set search_path = public
as $$
declare
    me      uuid := auth.uid();
    conv_id bigint;
begin
    if me is null then
        raise exception 'not authenticated';
    end if;
    if p_other is null or p_other = me then
        raise exception 'invalid conversation partner';
    end if;
    if not exists (select 1 from users where id = p_other) then
        raise exception 'user not found';
    end if;

    select a.conversation_id into conv_id
    from conversation_members a
    join conversation_members b on b.conversation_id = a.conversation_id and b.user_id = p_other
    join conversations c on c.id = a.conversation_id and not c.is_group
    where a.user_id = me
      and (select count(*) from conversation_members m where m.conversation_id = a.conversation_id) = 2
    limit 1;

    if conv_id is null then
        insert into conversations default values returning id into conv_id;
        insert into conversation_members (conversation_id, user_id) values (conv_id, me), (conv_id, p_other);
    end if;

    return conv_id;
end;
$$;

-- Gruppe anlegen: Ersteller + mindestens zwei weitere, höchstens 32 Mitglieder insgesamt
create or replace function public.create_group_conversation(p_title text, p_member_ids uuid[])
returns bigint
language plpgsql
security definer
set search_path = public
as $$
declare
    me      uuid := auth.uid();
    v_title text := trim(coalesce(p_title, ''));
    v_ids   uuid[];
    conv_id bigint;
begin
    if me is null then
        raise exception 'not authenticated';
    end if;
    if char_length(v_title) not between 1 and 60 then
        raise exception 'group name needs 1 to 60 characters';
    end if;

    select coalesce(array_agg(distinct u.id), '{}') into v_ids
    from users u
    where u.id = any (p_member_ids) and u.id <> me;

    if coalesce(array_length(v_ids, 1), 0) < 2 then
        raise exception 'a group needs at least two other members';
    end if;
    if array_length(v_ids, 1) > 31 then
        raise exception 'groups can have up to 32 members';
    end if;

    insert into conversations (is_group, title, created_by)
    values (true, v_title, me)
    returning id into conv_id;

    insert into conversation_members (conversation_id, user_id)
    select conv_id, x from unnest(v_ids || me) as x;

    return conv_id;
end;
$$;

-- Weitere Mitglieder hinzufügen (jedes Mitglied darf das)
create or replace function public.add_conversation_members(p_conversation_id bigint, p_member_ids uuid[])
returns void
language plpgsql
security definer
set search_path = public
as $$
begin
    if not exists (
        select 1 from conversations c
        join conversation_members m on m.conversation_id = c.id and m.user_id = auth.uid()
        where c.id = p_conversation_id and c.is_group
    ) then
        raise exception 'group not found';
    end if;

    insert into conversation_members (conversation_id, user_id)
    select p_conversation_id, u.id
    from users u
    where u.id = any (p_member_ids)
    on conflict do nothing;

    if (select count(*) from conversation_members where conversation_id = p_conversation_id) > 32 then
        raise exception 'groups can have up to 32 members';
    end if;
end;
$$;

create or replace function public.rename_conversation(p_conversation_id bigint, p_title text)
returns void
language plpgsql
security definer
set search_path = public
as $$
begin
    if char_length(trim(coalesce(p_title, ''))) not between 1 and 60 then
        raise exception 'group name needs 1 to 60 characters';
    end if;

    update conversations c
    set title = trim(p_title)
    where c.id = p_conversation_id
      and c.is_group
      and exists (select 1 from conversation_members m where m.conversation_id = c.id and m.user_id = auth.uid());

    if not found then
        raise exception 'group not found';
    end if;
end;
$$;

-- Gruppe verlassen; verlässt das letzte Mitglied die Gruppe, wird sie gelöscht
create or replace function public.leave_conversation(p_conversation_id bigint)
returns void
language plpgsql
security definer
set search_path = public
as $$
begin
    if not exists (select 1 from conversations where id = p_conversation_id and is_group) then
        raise exception 'group not found';
    end if;

    delete from conversation_members
    where conversation_id = p_conversation_id and user_id = auth.uid();

    if not found then
        raise exception 'group not found';
    end if;

    delete from conversations c
    where c.id = p_conversation_id
      and not exists (select 1 from conversation_members m where m.conversation_id = c.id);
end;
$$;

-- Profile aller Mitglieder (für Absendernamen in Gruppen). RLS auf
-- conversation_members (016) beschränkt das auf Mitglieder.
create or replace function public.conversation_member_profiles(p_conversation_id bigint)
returns setof public.users
language sql
stable
security invoker
set search_path = public
as $$
    select u.*
    from conversation_members m
    join users u on u.id = m.user_id
    where m.conversation_id = p_conversation_id
    order by u.username;
$$;

revoke execute on function public.create_group_conversation(text, uuid[])          from public, anon;
revoke execute on function public.add_conversation_members(bigint, uuid[])         from public, anon;
revoke execute on function public.rename_conversation(bigint, text)                from public, anon;
revoke execute on function public.leave_conversation(bigint)                       from public, anon;
grant  execute on function public.create_group_conversation(text, uuid[])          to authenticated;
grant  execute on function public.add_conversation_members(bigint, uuid[])         to authenticated;
grant  execute on function public.rename_conversation(bigint, text)                to authenticated;
grant  execute on function public.leave_conversation(bigint)                       to authenticated;

-- ---------------------------------------------------------------------
-- Nachrichten: Bilder, bearbeiten, löschen
-- ---------------------------------------------------------------------

-- image_path: Pfad im privaten Bucket "chat-images" (<conversation_id>/<user_id>/<datei>)
-- updated_at: Cursor für das Polling – Bearbeiten/Löschen kommt so auch beim Gegenüber an
alter table public.messages
    add column if not exists image_path text,
    add column if not exists edited_at  timestamptz,
    add column if not exists deleted_at timestamptz,
    add column if not exists updated_at timestamptz not null default now();

create index if not exists messages_conversation_updated_idx on public.messages (conversation_id, updated_at);

drop trigger if exists messages_set_updated_at on public.messages;
create trigger messages_set_updated_at
    before update on public.messages
    for each row execute function public.set_updated_at();

-- Text darf bei Bildern leer sein, gelöschte Nachrichten sind leer
alter table public.messages drop constraint if exists messages_content_check;
do $$
begin
    if not exists (select 1 from pg_constraint where conname = 'messages_content_valid') then
        alter table public.messages
            add constraint messages_content_valid check (
                char_length(content) <= 2000
                and (char_length(content) >= 1 or image_path is not null or deleted_at is not null)
            );
    end if;
end;
$$;

-- Bildpfad muss zur Unterhaltung und zum Absender passen
drop policy if exists "messages_insert_member" on public.messages;
create policy "messages_insert_member" on public.messages
    for insert with check (
        sender_id = auth.uid()
        and public.is_conversation_member(conversation_id)
        and story_id is null
        and story_url is null
        and (
            image_path is null
            or (split_part(image_path, '/', 1) = conversation_id::text
                and split_part(image_path, '/', 2) = auth.uid()::text)
        )
    );

-- Eigene Nachrichten ändern – nur Text, Bild und Bearbeitungs-/Löschzeitpunkt
revoke update on public.messages from anon, authenticated;
grant update (content, image_path, edited_at, deleted_at) on public.messages to authenticated;

drop policy if exists "messages_update_own" on public.messages;
create policy "messages_update_own" on public.messages
    for update
    using (sender_id = auth.uid())
    with check (
        sender_id = auth.uid()
        and (image_path is null or split_part(image_path, '/', 1) = conversation_id::text)
    );

create or replace function public.edit_message(p_message_id bigint, p_content text)
returns void
language plpgsql
security invoker
set search_path = public
as $$
declare
    v_content text := trim(coalesce(p_content, ''));
begin
    update messages
    set content = v_content,
        edited_at = now()
    where id = p_message_id
      and sender_id = auth.uid()
      and deleted_at is null
      and char_length(v_content) <= 2000
      and (char_length(v_content) >= 1 or image_path is not null);

    if not found then
        raise exception 'message not found';
    end if;
end;
$$;

-- Löscht weich (Platzhalter "Message deleted" bleibt) und liefert den alten
-- Bildpfad, damit die App die Datei aus dem Storage entfernen kann.
create or replace function public.delete_message(p_message_id bigint)
returns text
language plpgsql
security invoker
set search_path = public
as $$
declare
    v_path text;
begin
    select image_path into v_path
    from messages
    where id = p_message_id and sender_id = auth.uid() and deleted_at is null;

    if not found then
        raise exception 'message not found';
    end if;

    update messages
    set content = '',
        image_path = null,
        deleted_at = now()
    where id = p_message_id and sender_id = auth.uid();

    return v_path;
end;
$$;

-- Ungelesen zählt keine gelöschten Nachrichten; Gruppen erscheinen auch ohne Nachrichten.
-- Spalten aus 016 unverändert am Anfang, neue am Ende.
create or replace view public.conversation_list
with (security_invoker = true)
as
select
    c.id,
    c.last_message_at,
    o.user_id as other_user_id,
    u.username,
    u.display_name,
    u.vorname,
    u.nachname,
    u.profile_image_url,
    u.is_verified,
    coalesce(lm.content, '') as last_message,
    lm.sender_id as last_sender_id,
    (
        select count(*) from public.messages m
        where m.conversation_id = c.id
          and m.sender_id <> auth.uid()
          and m.created_at > me.last_read_at
          and m.deleted_at is null
    )::integer as unread_count,
    c.is_group,
    c.title,
    (select count(*) from public.conversation_members cm where cm.conversation_id = c.id)::integer as member_count,
    (lm.image_path is not null) as last_is_image,
    (lm.deleted_at is not null) as last_is_deleted,
    coalesce(nullif(ls.vorname, ''), nullif(ls.display_name, ''), ls.username) as last_sender_name
from public.conversations c
join public.conversation_members me on me.conversation_id = c.id and me.user_id = auth.uid()
left join lateral (
    select cm.user_id from public.conversation_members cm
    where cm.conversation_id = c.id and cm.user_id <> auth.uid()
    order by cm.user_id
    limit 1
) o on true
left join public.users u on u.id = o.user_id
left join lateral (
    select m.content, m.sender_id, m.image_path, m.deleted_at from public.messages m
    where m.conversation_id = c.id
    order by m.created_at desc
    limit 1
) lm on true
left join public.users ls on ls.id = lm.sender_id
where lm.sender_id is not null or c.is_group;

-- ---------------------------------------------------------------------
-- Storage: privater Bucket für Chat-Bilder (nur Mitglieder, per Signed URL)
-- ---------------------------------------------------------------------

insert into storage.buckets (id, name, public, file_size_limit, allowed_mime_types)
values ('chat-images', 'chat-images', false, 10485760, array['image/jpeg', 'image/png', 'image/webp'])
on conflict (id) do update
set public = false,
    file_size_limit = excluded.file_size_limit,
    allowed_mime_types = excluded.allowed_mime_types;

-- Erster Ordner = conversation_id. Prüft das Format, bevor gecastet wird.
create or replace function public.can_access_chat_file(p_name text)
returns boolean
language plpgsql
stable
security definer
set search_path = public
as $$
declare
    v_folder text := split_part(p_name, '/', 1);
begin
    if v_folder !~ '^[0-9]{1,18}$' then
        return false;
    end if;
    return exists (
        select 1 from conversation_members
        where conversation_id = v_folder::bigint and user_id = auth.uid()
    );
end;
$$;

drop policy if exists "chat_images_select_member" on storage.objects;
create policy "chat_images_select_member" on storage.objects
    for select to authenticated
    using (bucket_id = 'chat-images' and public.can_access_chat_file(name));

drop policy if exists "chat_images_insert_member" on storage.objects;
create policy "chat_images_insert_member" on storage.objects
    for insert to authenticated
    with check (
        bucket_id = 'chat-images'
        and public.can_access_chat_file(name)
        and split_part(name, '/', 2) = auth.uid()::text
    );

drop policy if exists "chat_images_delete_own" on storage.objects;
create policy "chat_images_delete_own" on storage.objects
    for delete to authenticated
    using (bucket_id = 'chat-images' and split_part(name, '/', 2) = auth.uid()::text);
