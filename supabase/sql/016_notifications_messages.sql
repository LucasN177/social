-- 016_notifications_messages.sql
-- Phase 5: Benachrichtigungen lesen/markieren und Direktnachrichten (1:1).

-- ---------------------------------------------------------------------
-- Benachrichtigungen (Tabelle + Trigger aus 007)
-- ---------------------------------------------------------------------

-- Benachrichtigungen inkl. Auslöser und Vorschau des betroffenen Posts.
-- RLS aus 007: nur der Empfänger sieht seine Zeilen.
create or replace view public.notification_feed
with (security_invoker = true)
as
select
    n.id,
    n.user_id,
    n.actor_id,
    n.type,
    n.post_id,
    n.message,
    n.is_read,
    n.created_at,
    u.username,
    u.display_name,
    u.vorname,
    u.nachname,
    u.profile_image_url,
    u.is_verified,
    (select pi.url from public.post_images pi where pi.post_id = n.post_id order by pi.position limit 1) as post_preview_url,
    p.media_type as post_media_type
from public.notifications n
join public.users u on u.id = n.actor_id
left join public.posts p on p.id = n.post_id;

create or replace function public.unread_notification_count()
returns integer
language sql
stable
security invoker
set search_path = public
as $$
    select count(*)::integer from notifications where user_id = auth.uid() and not is_read;
$$;

create or replace function public.mark_notifications_read()
returns void
language sql
security invoker
set search_path = public
as $$
    update notifications set is_read = true where user_id = auth.uid() and not is_read;
$$;

-- ---------------------------------------------------------------------
-- Direktnachrichten
-- ---------------------------------------------------------------------

create table if not exists public.conversations (
    id              bigint generated always as identity primary key,
    created_at      timestamptz not null default now(),
    last_message_at timestamptz not null default now()
);

create table if not exists public.conversation_members (
    conversation_id bigint not null references public.conversations (id) on delete cascade,
    user_id         uuid not null references public.users (id) on delete cascade,
    last_read_at    timestamptz not null default now(),
    primary key (conversation_id, user_id)
);

create index if not exists conversation_members_user_idx on public.conversation_members (user_id);

create table if not exists public.messages (
    id              bigint generated always as identity primary key,
    conversation_id bigint not null references public.conversations (id) on delete cascade,
    sender_id       uuid not null references public.users (id) on delete cascade,
    content         text not null check (char_length(content) between 1 and 2000),
    created_at      timestamptz not null default now()
);

create index if not exists messages_conversation_idx on public.messages (conversation_id, created_at desc);

-- Mitgliedschaft prüfen ohne RLS-Rekursion (Policy auf conversation_members würde sich sonst selbst abfragen)
create or replace function public.is_conversation_member(p_conversation_id bigint)
returns boolean
language sql
stable
security definer
set search_path = public
as $$
    select exists (
        select 1 from conversation_members
        where conversation_id = p_conversation_id and user_id = auth.uid()
    );
$$;

alter table public.conversations        enable row level security;
alter table public.conversation_members enable row level security;
alter table public.messages             enable row level security;

drop policy if exists "conversations_select_member" on public.conversations;
create policy "conversations_select_member" on public.conversations
    for select using (public.is_conversation_member(id));

drop policy if exists "conversation_members_select_member" on public.conversation_members;
create policy "conversation_members_select_member" on public.conversation_members
    for select using (public.is_conversation_member(conversation_id));

-- Nur den eigenen Lesestand ändern
drop policy if exists "conversation_members_update_own" on public.conversation_members;
create policy "conversation_members_update_own" on public.conversation_members
    for update using (user_id = auth.uid()) with check (user_id = auth.uid());

drop policy if exists "messages_select_member" on public.messages;
create policy "messages_select_member" on public.messages
    for select using (public.is_conversation_member(conversation_id));

drop policy if exists "messages_insert_member" on public.messages;
create policy "messages_insert_member" on public.messages
    for insert with check (sender_id = auth.uid() and public.is_conversation_member(conversation_id));

drop policy if exists "messages_delete_own" on public.messages;
create policy "messages_delete_own" on public.messages
    for delete using (sender_id = auth.uid());

-- Unterhaltung nach jeder Nachricht nach oben sortieren
create or replace function public.touch_conversation()
returns trigger
language plpgsql
security definer
set search_path = public
as $$
begin
    update conversations set last_message_at = new.created_at where id = new.conversation_id;
    return null;
end;
$$;

drop trigger if exists messages_touch_conversation on public.messages;
create trigger messages_touch_conversation
    after insert on public.messages
    for each row execute function public.touch_conversation();

-- 1:1-Unterhaltung mit einem anderen User finden oder anlegen.
-- security definer, weil auch die Mitgliedschaft des Gegenübers angelegt wird.
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
    where a.user_id = me
      and (select count(*) from conversation_members c where c.conversation_id = a.conversation_id) = 2
    limit 1;

    if conv_id is null then
        insert into conversations default values returning id into conv_id;
        insert into conversation_members (conversation_id, user_id) values (conv_id, me), (conv_id, p_other);
    end if;

    return conv_id;
end;
$$;

-- Chatliste: nur Unterhaltungen mit mindestens einer Nachricht, inkl. Gegenüber und Ungelesen-Zähler
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
    lm.content   as last_message,
    lm.sender_id as last_sender_id,
    (
        select count(*) from public.messages m
        where m.conversation_id = c.id and m.sender_id <> auth.uid() and m.created_at > me.last_read_at
    )::integer as unread_count
from public.conversations c
join public.conversation_members me on me.conversation_id = c.id and me.user_id = auth.uid()
join public.conversation_members o  on o.conversation_id = c.id and o.user_id <> auth.uid()
join public.users u on u.id = o.user_id
join lateral (
    select m.content, m.sender_id from public.messages m
    where m.conversation_id = c.id
    order by m.created_at desc
    limit 1
) lm on true;

create or replace function public.mark_conversation_read(p_conversation_id bigint)
returns void
language sql
security invoker
set search_path = public
as $$
    update conversation_members set last_read_at = now()
    where conversation_id = p_conversation_id and user_id = auth.uid();
$$;

create or replace function public.unread_message_count()
returns integer
language sql
stable
security invoker
set search_path = public
as $$
    select coalesce(sum(unread_count), 0)::integer from conversation_list;
$$;

-- Für künftige Realtime-Abos (siehe 011_realtime.sql)
do $$
begin
    if not exists (
        select 1 from pg_publication_tables
        where pubname = 'supabase_realtime' and schemaname = 'public' and tablename = 'messages'
    ) then
        alter publication supabase_realtime add table public.messages;
    end if;
end;
$$;
