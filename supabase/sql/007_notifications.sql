-- 007_notifications.sql
-- Benachrichtigungen; werden per Trigger bei Like, Kommentar und Follow erzeugt.
-- type entspricht dem C#-Enum NotificationType (like, comment, follow, mention, message).

create table if not exists public.notifications (
    id         bigint generated always as identity primary key,
    user_id    uuid not null references public.users (id) on delete cascade,   -- Empfänger
    actor_id   uuid not null references public.users (id) on delete cascade,   -- Auslöser
    type       text not null check (type in ('like', 'comment', 'follow', 'mention', 'message')),
    post_id    bigint references public.posts (id) on delete cascade,
    message    text not null default '',
    is_read    boolean not null default false,
    created_at timestamptz not null default now()
);

create index if not exists notifications_user_idx
    on public.notifications (user_id, is_read, created_at desc);

-- RLS: nur der Empfänger darf lesen / als gelesen markieren / löschen.
-- Inserts kommen ausschließlich aus den security-definer-Triggern unten.
alter table public.notifications enable row level security;

drop policy if exists "notifications_select_own" on public.notifications;
create policy "notifications_select_own" on public.notifications
    for select using (auth.uid() = user_id);

drop policy if exists "notifications_update_own" on public.notifications;
create policy "notifications_update_own" on public.notifications
    for update using (auth.uid() = user_id) with check (auth.uid() = user_id);

drop policy if exists "notifications_delete_own" on public.notifications;
create policy "notifications_delete_own" on public.notifications
    for delete using (auth.uid() = user_id);

-- Trigger-Funktionen
create or replace function public.notify_on_like()
returns trigger
language plpgsql
security definer
set search_path = public
as $$
declare
    owner_id uuid;
begin
    select user_id into owner_id from posts where id = new.post_id;
    if owner_id is not null and owner_id <> new.user_id then
        insert into notifications (user_id, actor_id, type, post_id, message)
        values (owner_id, new.user_id, 'like', new.post_id, 'liked your post');
    end if;
    return new;
end;
$$;

create or replace function public.notify_on_comment()
returns trigger
language plpgsql
security definer
set search_path = public
as $$
declare
    owner_id uuid;
begin
    select user_id into owner_id from posts where id = new.post_id;
    if owner_id is not null and owner_id <> new.user_id then
        insert into notifications (user_id, actor_id, type, post_id, message)
        values (owner_id, new.user_id, 'comment', new.post_id, 'commented on your post');
    end if;
    return new;
end;
$$;

create or replace function public.notify_on_follow()
returns trigger
language plpgsql
security definer
set search_path = public
as $$
begin
    insert into notifications (user_id, actor_id, type, message)
    values (new.following_id, new.follower_id, 'follow', 'started following you');
    return new;
end;
$$;

drop trigger if exists likes_notify on public.likes;
create trigger likes_notify
    after insert on public.likes
    for each row execute function public.notify_on_like();

drop trigger if exists comments_notify on public.comments;
create trigger comments_notify
    after insert on public.comments
    for each row execute function public.notify_on_comment();

drop trigger if exists follows_notify on public.follows;
create trigger follows_notify
    after insert on public.follows
    for each row execute function public.notify_on_follow();
