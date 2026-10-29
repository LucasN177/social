-- =====================================================================
-- setup.sql - komplettes Datenbank-Setup fuer Social (Supabase)
-- AUTOMATISCH ERZEUGT von supabase/build-setup.ps1 - nicht von Hand bearbeiten.
-- Quelle: supabase/sql/000_extensions.sql bis 015_stories_reels.sql
--
-- Ausfuehren: Supabase Dashboard > SQL Editor > Inhalt einfuegen > Run.
-- Idempotent: kann beliebig oft erneut ausgefuehrt werden.
-- Laeuft in einer Transaktion - bei einem Fehler wird nichts uebernommen.
-- =====================================================================

begin;

-- ---------------------------------------------------------------------
-- 000_extensions.sql
-- Extensions und gemeinsame Hilfsfunktionen

create extension if not exists pgcrypto;
create extension if not exists pg_trgm;   -- unscharfe Suche nach Usernamen / Hashtags

-- Setzt updated_at automatisch bei jedem UPDATE
create or replace function public.set_updated_at()
returns trigger
language plpgsql
as $$
begin
    new.updated_at = now();
    return new;
end;
$$;

-- ---------------------------------------------------------------------
-- 001_users.sql
-- Öffentliches Profil je Auth-User. Wird per Trigger beim Registrieren angelegt,
-- die App ergänzt danach Username / Vorname / Nachname (AuthRepository.InsertUserMetadata).

create table if not exists public.users (
    id                uuid primary key references auth.users (id) on delete cascade,
    username          text unique,
    display_name      text not null default '',
    vorname           text not null default '',
    nachname          text not null default '',
    bio               text not null default '',
    profile_image_url text not null default '',
    followers_count   integer not null default 0,
    following_count   integer not null default 0,
    posts_count       integer not null default 0,
    is_verified       boolean not null default false,
    created_at        timestamptz not null default now(),
    updated_at        timestamptz not null default now()
);

-- Falls die Tabelle schon existiert: fehlende Spalten nachziehen
alter table public.users add column if not exists display_name      text not null default '';
alter table public.users add column if not exists vorname           text not null default '';
alter table public.users add column if not exists nachname          text not null default '';
alter table public.users add column if not exists bio               text not null default '';
alter table public.users add column if not exists profile_image_url text not null default '';
alter table public.users add column if not exists followers_count   integer not null default 0;
alter table public.users add column if not exists following_count   integer not null default 0;
alter table public.users add column if not exists posts_count       integer not null default 0;
alter table public.users add column if not exists is_verified       boolean not null default false;
alter table public.users add column if not exists updated_at        timestamptz not null default now();

create index if not exists users_username_trgm_idx
    on public.users using gin (username gin_trgm_ops);

drop trigger if exists users_set_updated_at on public.users;
create trigger users_set_updated_at
    before update on public.users
    for each row execute function public.set_updated_at();

-- Profilzeile automatisch anlegen, sobald sich jemand registriert
create or replace function public.handle_new_user()
returns trigger
language plpgsql
security definer
set search_path = public
as $$
begin
    -- Die App übergibt username / vorname / nachname / display_name beim SignUp als User-Metadaten
    insert into public.users (id, username, display_name, vorname, nachname)
    values (
        new.id,
        nullif(new.raw_user_meta_data ->> 'username', ''),
        coalesce(new.raw_user_meta_data ->> 'display_name', ''),
        coalesce(new.raw_user_meta_data ->> 'vorname', ''),
        coalesce(new.raw_user_meta_data ->> 'nachname', '')
    )
    on conflict (id) do nothing;
    return new;
end;
$$;

drop trigger if exists on_auth_user_created on auth.users;
create trigger on_auth_user_created
    after insert on auth.users
    for each row execute function public.handle_new_user();

-- Bereits registrierte User ohne Profilzeile nachtragen
insert into public.users (id)
select a.id from auth.users a
on conflict (id) do nothing;

-- RLS
alter table public.users enable row level security;

drop policy if exists "users_select_all" on public.users;
create policy "users_select_all" on public.users
    for select using (true);

drop policy if exists "users_update_own" on public.users;
create policy "users_update_own" on public.users
    for update using (auth.uid() = id) with check (auth.uid() = id);

-- ---------------------------------------------------------------------
-- 002_posts.sql
-- Beiträge und zugehörige Bilder / Videos

create table if not exists public.posts (
    id             bigint generated always as identity primary key,
    user_id        uuid not null references public.users (id) on delete cascade,
    content        text not null default '',
    location       text not null default '',
    tags           text[] not null default '{}',
    media_type     text not null default 'image' check (media_type in ('image', 'video', 'text')),
    likes_count    integer not null default 0,
    comments_count integer not null default 0,
    shares_count   integer not null default 0,
    created_at     timestamptz not null default now(),
    updated_at     timestamptz not null default now()
);

create index if not exists posts_user_id_idx    on public.posts (user_id);
create index if not exists posts_created_at_idx on public.posts (created_at desc);
create index if not exists posts_tags_idx       on public.posts using gin (tags);

drop trigger if exists posts_set_updated_at on public.posts;
create trigger posts_set_updated_at
    before update on public.posts
    for each row execute function public.set_updated_at();

create table if not exists public.post_images (
    id         bigint generated always as identity primary key,
    post_id    bigint not null references public.posts (id) on delete cascade,
    url        text not null,
    position   smallint not null default 0,
    created_at timestamptz not null default now()
);

create index if not exists post_images_post_id_idx on public.post_images (post_id, position);

-- RLS
alter table public.posts       enable row level security;
alter table public.post_images enable row level security;

drop policy if exists "posts_select_all" on public.posts;
create policy "posts_select_all" on public.posts
    for select using (true);

drop policy if exists "posts_insert_own" on public.posts;
create policy "posts_insert_own" on public.posts
    for insert with check (auth.uid() = user_id);

drop policy if exists "posts_update_own" on public.posts;
create policy "posts_update_own" on public.posts
    for update using (auth.uid() = user_id) with check (auth.uid() = user_id);

drop policy if exists "posts_delete_own" on public.posts;
create policy "posts_delete_own" on public.posts
    for delete using (auth.uid() = user_id);

drop policy if exists "post_images_select_all" on public.post_images;
create policy "post_images_select_all" on public.post_images
    for select using (true);

drop policy if exists "post_images_write_own" on public.post_images;
create policy "post_images_write_own" on public.post_images
    for all
    using (exists (select 1 from public.posts p where p.id = post_id and p.user_id = auth.uid()))
    with check (exists (select 1 from public.posts p where p.id = post_id and p.user_id = auth.uid()));

-- ---------------------------------------------------------------------
-- 003_comments.sql
-- Kommentare zu Beiträgen und Likes auf Kommentare

create table if not exists public.comments (
    id          bigint generated always as identity primary key,
    post_id     bigint not null references public.posts (id) on delete cascade,
    user_id     uuid not null references public.users (id) on delete cascade,
    content     text not null check (char_length(content) between 1 and 2000),
    likes_count integer not null default 0,
    created_at  timestamptz not null default now()
);

create index if not exists comments_post_id_idx on public.comments (post_id, created_at);

create table if not exists public.comment_likes (
    comment_id bigint not null references public.comments (id) on delete cascade,
    user_id    uuid not null references public.users (id) on delete cascade,
    created_at timestamptz not null default now(),
    primary key (comment_id, user_id)
);

-- RLS
alter table public.comments      enable row level security;
alter table public.comment_likes enable row level security;

drop policy if exists "comments_select_all" on public.comments;
create policy "comments_select_all" on public.comments
    for select using (true);

drop policy if exists "comments_insert_own" on public.comments;
create policy "comments_insert_own" on public.comments
    for insert with check (auth.uid() = user_id);

drop policy if exists "comments_delete_own" on public.comments;
create policy "comments_delete_own" on public.comments
    for delete using (
        auth.uid() = user_id
        or exists (select 1 from public.posts p where p.id = post_id and p.user_id = auth.uid())
    );

drop policy if exists "comment_likes_select_all" on public.comment_likes;
create policy "comment_likes_select_all" on public.comment_likes
    for select using (true);

drop policy if exists "comment_likes_insert_own" on public.comment_likes;
create policy "comment_likes_insert_own" on public.comment_likes
    for insert with check (auth.uid() = user_id);

drop policy if exists "comment_likes_delete_own" on public.comment_likes;
create policy "comment_likes_delete_own" on public.comment_likes
    for delete using (auth.uid() = user_id);

-- ---------------------------------------------------------------------
-- 004_interactions.sql
-- Likes und Lesezeichen auf Beiträge

create table if not exists public.likes (
    post_id    bigint not null references public.posts (id) on delete cascade,
    user_id    uuid not null references public.users (id) on delete cascade,
    created_at timestamptz not null default now(),
    primary key (post_id, user_id)
);

create index if not exists likes_user_id_idx on public.likes (user_id);

create table if not exists public.bookmarks (
    post_id    bigint not null references public.posts (id) on delete cascade,
    user_id    uuid not null references public.users (id) on delete cascade,
    created_at timestamptz not null default now(),
    primary key (post_id, user_id)
);

create index if not exists bookmarks_user_id_idx on public.bookmarks (user_id, created_at desc);

-- RLS
alter table public.likes     enable row level security;
alter table public.bookmarks enable row level security;

drop policy if exists "likes_select_all" on public.likes;
create policy "likes_select_all" on public.likes
    for select using (true);

drop policy if exists "likes_insert_own" on public.likes;
create policy "likes_insert_own" on public.likes
    for insert with check (auth.uid() = user_id);

drop policy if exists "likes_delete_own" on public.likes;
create policy "likes_delete_own" on public.likes
    for delete using (auth.uid() = user_id);

-- Lesezeichen sind privat
drop policy if exists "bookmarks_own" on public.bookmarks;
create policy "bookmarks_own" on public.bookmarks
    for all using (auth.uid() = user_id) with check (auth.uid() = user_id);

-- ---------------------------------------------------------------------
-- 005_follows.sql
-- Social Graph: wer folgt wem

create table if not exists public.follows (
    follower_id  uuid not null references public.users (id) on delete cascade,
    following_id uuid not null references public.users (id) on delete cascade,
    created_at   timestamptz not null default now(),
    primary key (follower_id, following_id),
    check (follower_id <> following_id)
);

create index if not exists follows_following_id_idx on public.follows (following_id);

-- RLS
alter table public.follows enable row level security;

drop policy if exists "follows_select_all" on public.follows;
create policy "follows_select_all" on public.follows
    for select using (true);

drop policy if exists "follows_insert_own" on public.follows;
create policy "follows_insert_own" on public.follows
    for insert with check (auth.uid() = follower_id);

drop policy if exists "follows_delete_own" on public.follows;
create policy "follows_delete_own" on public.follows
    for delete using (auth.uid() = follower_id);

-- ---------------------------------------------------------------------
-- 006_stories.sql
-- Stories (24 Stunden sichtbar) und wer sie gesehen hat

create table if not exists public.stories (
    id         bigint generated always as identity primary key,
    user_id    uuid not null references public.users (id) on delete cascade,
    image_url  text not null,
    created_at timestamptz not null default now(),
    expires_at timestamptz not null default now() + interval '24 hours'
);

create index if not exists stories_user_expires_idx on public.stories (user_id, expires_at desc);

create table if not exists public.story_views (
    story_id  bigint not null references public.stories (id) on delete cascade,
    user_id   uuid not null references public.users (id) on delete cascade,
    viewed_at timestamptz not null default now(),
    primary key (story_id, user_id)
);

-- RLS
alter table public.stories     enable row level security;
alter table public.story_views enable row level security;

drop policy if exists "stories_select_active" on public.stories;
create policy "stories_select_active" on public.stories
    for select using (expires_at > now() or auth.uid() = user_id);

drop policy if exists "stories_insert_own" on public.stories;
create policy "stories_insert_own" on public.stories
    for insert with check (auth.uid() = user_id);

drop policy if exists "stories_delete_own" on public.stories;
create policy "stories_delete_own" on public.stories
    for delete using (auth.uid() = user_id);

-- Viewer sehen ihre eigenen Views, Story-Besitzer sehen alle Views ihrer Story
drop policy if exists "story_views_select" on public.story_views;
create policy "story_views_select" on public.story_views
    for select using (
        auth.uid() = user_id
        or exists (select 1 from public.stories s where s.id = story_id and s.user_id = auth.uid())
    );

drop policy if exists "story_views_insert_own" on public.story_views;
create policy "story_views_insert_own" on public.story_views
    for insert with check (auth.uid() = user_id);

-- ---------------------------------------------------------------------
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

-- ---------------------------------------------------------------------
-- 008_counters.sql
-- Hält die denormalisierten Zähler (likes_count, comments_count, posts_count,
-- followers_count, following_count) aktuell. security definer, weil die Zähler
-- in fremden Zeilen geändert werden, für die der User keine UPDATE-Policy hat.

create or replace function public.update_post_likes_count()
returns trigger
language plpgsql
security definer
set search_path = public
as $$
begin
    if tg_op = 'INSERT' then
        update posts set likes_count = likes_count + 1 where id = new.post_id;
    elsif tg_op = 'DELETE' then
        update posts set likes_count = greatest(likes_count - 1, 0) where id = old.post_id;
    end if;
    return null;
end;
$$;

create or replace function public.update_post_comments_count()
returns trigger
language plpgsql
security definer
set search_path = public
as $$
begin
    if tg_op = 'INSERT' then
        update posts set comments_count = comments_count + 1 where id = new.post_id;
    elsif tg_op = 'DELETE' then
        update posts set comments_count = greatest(comments_count - 1, 0) where id = old.post_id;
    end if;
    return null;
end;
$$;

create or replace function public.update_comment_likes_count()
returns trigger
language plpgsql
security definer
set search_path = public
as $$
begin
    if tg_op = 'INSERT' then
        update comments set likes_count = likes_count + 1 where id = new.comment_id;
    elsif tg_op = 'DELETE' then
        update comments set likes_count = greatest(likes_count - 1, 0) where id = old.comment_id;
    end if;
    return null;
end;
$$;

create or replace function public.update_user_posts_count()
returns trigger
language plpgsql
security definer
set search_path = public
as $$
begin
    if tg_op = 'INSERT' then
        update users set posts_count = posts_count + 1 where id = new.user_id;
    elsif tg_op = 'DELETE' then
        update users set posts_count = greatest(posts_count - 1, 0) where id = old.user_id;
    end if;
    return null;
end;
$$;

create or replace function public.update_follow_counts()
returns trigger
language plpgsql
security definer
set search_path = public
as $$
begin
    if tg_op = 'INSERT' then
        update users set following_count = following_count + 1 where id = new.follower_id;
        update users set followers_count = followers_count + 1 where id = new.following_id;
    elsif tg_op = 'DELETE' then
        update users set following_count = greatest(following_count - 1, 0) where id = old.follower_id;
        update users set followers_count = greatest(followers_count - 1, 0) where id = old.following_id;
    end if;
    return null;
end;
$$;

drop trigger if exists likes_count_trigger on public.likes;
create trigger likes_count_trigger
    after insert or delete on public.likes
    for each row execute function public.update_post_likes_count();

drop trigger if exists comments_count_trigger on public.comments;
create trigger comments_count_trigger
    after insert or delete on public.comments
    for each row execute function public.update_post_comments_count();

drop trigger if exists comment_likes_count_trigger on public.comment_likes;
create trigger comment_likes_count_trigger
    after insert or delete on public.comment_likes
    for each row execute function public.update_comment_likes_count();

drop trigger if exists posts_count_trigger on public.posts;
create trigger posts_count_trigger
    after insert or delete on public.posts
    for each row execute function public.update_user_posts_count();

drop trigger if exists follows_count_trigger on public.follows;
create trigger follows_count_trigger
    after insert or delete on public.follows
    for each row execute function public.update_follow_counts();

-- ---------------------------------------------------------------------
-- 009_views_and_functions.sql
-- Lese-Views und RPC-Funktionen für die App

-- Feed: Beiträge inkl. Autor, Bilder und Like-/Bookmark-Status des aktuellen Users.
-- security_invoker = true, damit die RLS-Policies der Basistabellen greifen.
-- Spalten identisch mit 013_posts_api.sql halten: "create or replace view" kann keine Spalten
-- entfernen, sonst scheitert ein erneutes Ausführen aller Skripte (setup.sql).
create or replace view public.feed_posts
with (security_invoker = true)
as
select
    p.id,
    p.user_id,
    p.content,
    p.location,
    p.tags,
    p.media_type,
    p.likes_count,
    p.comments_count,
    p.shares_count,
    p.created_at,
    u.username,
    u.display_name,
    u.profile_image_url,
    u.is_verified,
    coalesce(
        (select array_agg(pi.url order by pi.position) from public.post_images pi where pi.post_id = p.id),
        '{}'
    ) as image_urls,
    exists (select 1 from public.likes l     where l.post_id = p.id and l.user_id = auth.uid()) as is_liked,
    exists (select 1 from public.bookmarks b where b.post_id = p.id and b.user_id = auth.uid()) as is_bookmarked,
    u.vorname,
    u.nachname
from public.posts p
join public.users u on u.id = p.user_id;

-- Vorschläge: User, denen man noch nicht folgt, sortiert nach Followern
create or replace function public.suggested_users(max_results integer default 5)
returns setof public.users
language sql
stable
security invoker
set search_path = public
as $$
    select u.*
    from users u
    where u.id <> auth.uid()
      and not exists (
          select 1 from follows f
          where f.follower_id = auth.uid() and f.following_id = u.id
      )
    order by u.followers_count desc, u.created_at desc
    limit max_results;
$$;

-- User-Suche über Username / Anzeigename (nutzt den Trigram-Index)
create or replace function public.search_users(term text, max_results integer default 20)
returns setof public.users
language sql
stable
security invoker
set search_path = public
as $$
    select *
    from users
    where username ilike '%' || term || '%'
       or display_name ilike '%' || term || '%'
    order by similarity(coalesce(username, ''), term) desc
    limit max_results;
$$;

-- ---------------------------------------------------------------------
-- 010_storage.sql
-- Storage-Buckets für Profilbilder, Beitragsbilder und Stories.
-- Konvention für Pfade: <user_id>/<dateiname>, damit die Policies den Besitzer erkennen.

insert into storage.buckets (id, name, public)
values
    ('avatars',     'avatars',     true),
    ('post-images', 'post-images', true),
    ('stories',     'stories',     true)
on conflict (id) do nothing;

drop policy if exists "media_public_read" on storage.objects;
create policy "media_public_read" on storage.objects
    for select using (bucket_id in ('avatars', 'post-images', 'stories'));

drop policy if exists "media_insert_own_folder" on storage.objects;
create policy "media_insert_own_folder" on storage.objects
    for insert to authenticated
    with check (
        bucket_id in ('avatars', 'post-images', 'stories')
        and (storage.foldername(name))[1] = auth.uid()::text
    );

drop policy if exists "media_update_own_folder" on storage.objects;
create policy "media_update_own_folder" on storage.objects
    for update to authenticated
    using (
        bucket_id in ('avatars', 'post-images', 'stories')
        and (storage.foldername(name))[1] = auth.uid()::text
    );

drop policy if exists "media_delete_own_folder" on storage.objects;
create policy "media_delete_own_folder" on storage.objects
    for delete to authenticated
    using (
        bucket_id in ('avatars', 'post-images', 'stories')
        and (storage.foldername(name))[1] = auth.uid()::text
    );

-- ---------------------------------------------------------------------
-- 011_realtime.sql
-- Realtime-Events für Benachrichtigungen aktivieren (RLS gilt auch für Realtime).

do $$
begin
    if not exists (
        select 1 from pg_publication_tables
        where pubname = 'supabase_realtime'
          and schemaname = 'public'
          and tablename = 'notifications'
    ) then
        alter publication supabase_realtime add table public.notifications;
    end if;
end;
$$;

-- ---------------------------------------------------------------------
-- 012_users_profile_rules.sql
-- Die Policy users_update_own (001) erlaubt das Ändern der eigenen Zeile – ohne diese Rechte
-- könnte ein Client auch followers_count, posts_count oder is_verified setzen.
-- Nur die Profilfelder bleiben für eingeloggte User änderbar; die Zähler-Trigger (008)
-- laufen als security definer und sind davon nicht betroffen.

revoke update on public.users from anon, authenticated;
grant update (username, display_name, vorname, nachname, bio, profile_image_url)
    on public.users to authenticated;

-- Format-Regeln wie in der App (Pages/Login.razor, Pages/Dialogs/EditProfileDialog.razor).
-- "not valid": bestehende Zeilen werden nicht geprüft, nur neue bzw. geänderte.
do $$
begin
    if not exists (select 1 from pg_constraint where conname = 'users_username_format') then
        alter table public.users
            add constraint users_username_format check (username ~ '^[a-z0-9._]{3,30}$') not valid;
    end if;

    if not exists (select 1 from pg_constraint where conname = 'users_bio_length') then
        alter table public.users
            add constraint users_bio_length check (char_length(bio) <= 160) not valid;
    end if;
end;
$$;

-- ---------------------------------------------------------------------
-- 013_posts_api.sql
-- Lese- und Schreib-API für Phase 2 (Posts, Kommentare).

-- Feed um Vor-/Nachname erweitern (für Initialen-Avatare).
-- "create or replace view" erlaubt neue Spalten nur am Ende.
create or replace view public.feed_posts
with (security_invoker = true)
as
select
    p.id,
    p.user_id,
    p.content,
    p.location,
    p.tags,
    p.media_type,
    p.likes_count,
    p.comments_count,
    p.shares_count,
    p.created_at,
    u.username,
    u.display_name,
    u.profile_image_url,
    u.is_verified,
    coalesce(
        (select array_agg(pi.url order by pi.position) from public.post_images pi where pi.post_id = p.id),
        '{}'
    ) as image_urls,
    exists (select 1 from public.likes l     where l.post_id = p.id and l.user_id = auth.uid()) as is_liked,
    exists (select 1 from public.bookmarks b where b.post_id = p.id and b.user_id = auth.uid()) as is_bookmarked,
    u.vorname,
    u.nachname
from public.posts p
join public.users u on u.id = p.user_id;

-- Kommentare inkl. Autor
create or replace view public.comment_details
with (security_invoker = true)
as
select
    c.id,
    c.post_id,
    c.user_id,
    c.content,
    c.likes_count,
    c.created_at,
    u.username,
    u.display_name,
    u.vorname,
    u.nachname,
    u.profile_image_url,
    u.is_verified
from public.comments c
join public.users u on u.id = c.user_id;

-- Post + Bilder in einer Transaktion anlegen. security invoker: die RLS-Policies
-- aus 002_posts.sql gelten weiterhin (nur eigene Posts / Bilder).
create or replace function public.create_post(
    p_content    text,
    p_location   text default '',
    p_tags       text[] default '{}',
    p_image_urls text[] default '{}'
)
returns bigint
language plpgsql
security invoker
set search_path = public
as $$
declare
    new_id bigint;
begin
    if auth.uid() is null then
        raise exception 'not authenticated';
    end if;

    if coalesce(trim(p_content), '') = '' and coalesce(array_length(p_image_urls, 1), 0) = 0 then
        raise exception 'post needs text or at least one image';
    end if;

    if coalesce(array_length(p_image_urls, 1), 0) > 10 then
        raise exception 'at most 10 images per post';
    end if;

    insert into posts (user_id, content, location, tags, media_type)
    values (
        auth.uid(),
        coalesce(p_content, ''),
        coalesce(p_location, ''),
        coalesce(p_tags, '{}'),
        case when coalesce(array_length(p_image_urls, 1), 0) > 0 then 'image' else 'text' end
    )
    returning id into new_id;

    insert into post_images (post_id, url, position)
    select new_id, url, (ord - 1)::smallint
    from unnest(p_image_urls) with ordinality as t(url, ord);

    return new_id;
end;
$$;

-- Länge des Beitragstexts begrenzen (wie in der App)
do $$
begin
    if not exists (select 1 from pg_constraint where conname = 'posts_content_length') then
        alter table public.posts
            add constraint posts_content_length check (char_length(content) <= 2200) not valid;
    end if;
end;
$$;

-- ---------------------------------------------------------------------
-- 014_social_graph.sql
-- Phase 3: Home-Feed aus gefolgten Accounts, Hashtag-Suche, Follower-Listen.

-- Home-Feed: eigene Posts + Posts von Accounts, denen man folgt.
-- feed_posts (013) ist selbst security_invoker, RLS greift also weiterhin.
create or replace view public.home_feed
with (security_invoker = true)
as
select f.*
from public.feed_posts f
where f.user_id = auth.uid()
   or exists (
       select 1 from public.follows fo
       where fo.follower_id = auth.uid() and fo.following_id = f.user_id
   );

-- Hashtags nach Präfix suchen, sortiert nach Häufigkeit
create or replace function public.search_hashtags(term text, max_results integer default 20)
returns table (tag text, post_count bigint)
language sql
stable
security invoker
set search_path = public
as $$
    select t.tag, count(*) as post_count
    from posts p
    cross join lateral unnest(p.tags) as t(tag)
    where t.tag like replace(lower(trim(term)), '#', '') || '%'
    group by t.tag
    order by count(*) desc, t.tag
    limit max_results;
$$;

-- Follower / Gefolgte eines Users (für die Listen im Profil)
create or replace function public.list_followers(target uuid, max_results integer default 50)
returns setof public.users
language sql
stable
security invoker
set search_path = public
as $$
    select u.*
    from follows f
    join users u on u.id = f.follower_id
    where f.following_id = target
    order by f.created_at desc
    limit max_results;
$$;

create or replace function public.list_following(target uuid, max_results integer default 50)
returns setof public.users
language sql
stable
security invoker
set search_path = public
as $$
    select u.*
    from follows f
    join users u on u.id = f.following_id
    where f.follower_id = target
    order by f.created_at desc
    limit max_results;
$$;

-- Hashtag-Filter im Feed beschleunigen (Index existiert seit 002, hier nur zur Sicherheit)
create index if not exists posts_tags_idx on public.posts using gin (tags);

-- ---------------------------------------------------------------------
-- 015_stories_reels.sql
-- Phase 4: Stories (24 h) und Reels (Video-Posts).

-- ---------------------------------------------------------------------
-- Stories
-- ---------------------------------------------------------------------

-- Aktive Stories von mir + gefolgten Accounts, inkl. Autor und Gesehen-Status.
-- view_count: Dank RLS auf story_views (006) sieht nur der Story-Besitzer alle Views.
create or replace view public.story_feed
with (security_invoker = true)
as
select
    s.id,
    s.user_id,
    s.image_url,
    s.created_at,
    s.expires_at,
    u.username,
    u.display_name,
    u.vorname,
    u.nachname,
    u.profile_image_url,
    u.is_verified,
    exists (select 1 from public.story_views v where v.story_id = s.id and v.user_id = auth.uid()) as is_viewed,
    (select count(*) from public.story_views v where v.story_id = s.id)::integer as view_count
from public.stories s
join public.users u on u.id = s.user_id
where s.expires_at > now()
  and (
      s.user_id = auth.uid()
      or exists (select 1 from public.follows f where f.follower_id = auth.uid() and f.following_id = s.user_id)
  );

-- Story als gesehen markieren (mehrfaches Ansehen zählt einmal)
create or replace function public.mark_story_viewed(p_story_id bigint)
returns void
language sql
security invoker
set search_path = public
as $$
    insert into story_views (story_id, user_id)
    values (p_story_id, auth.uid())
    on conflict (story_id, user_id) do nothing;
$$;

-- Wer hat eine Story gesehen? RLS (006) beschränkt das auf den Besitzer.
create or replace function public.story_viewers(p_story_id bigint)
returns setof public.users
language sql
stable
security invoker
set search_path = public
as $$
    select u.*
    from story_views v
    join users u on u.id = v.user_id
    where v.story_id = p_story_id
    order by v.viewed_at desc;
$$;

create index if not exists stories_expires_idx on public.stories (expires_at);

-- ---------------------------------------------------------------------
-- Reels (Posts mit media_type = 'video'; die Video-URL liegt in post_images)
-- ---------------------------------------------------------------------

create or replace function public.create_reel(
    p_content   text,
    p_tags      text[] default '{}',
    p_video_url text default null
)
returns bigint
language plpgsql
security invoker
set search_path = public
as $$
declare
    new_id bigint;
begin
    if auth.uid() is null then
        raise exception 'not authenticated';
    end if;

    if coalesce(trim(p_video_url), '') = '' then
        raise exception 'reel needs a video';
    end if;

    insert into posts (user_id, content, location, tags, media_type)
    values (auth.uid(), coalesce(p_content, ''), '', coalesce(p_tags, '{}'), 'video')
    returning id into new_id;

    insert into post_images (post_id, url, position)
    values (new_id, p_video_url, 0);

    return new_id;
end;
$$;

create index if not exists posts_media_type_idx on public.posts (media_type, created_at desc);

-- ---------------------------------------------------------------------
-- Storage: Bucket für Reels + Limits für Stories
-- ---------------------------------------------------------------------

insert into storage.buckets (id, name, public)
values ('reels', 'reels', true)
on conflict (id) do nothing;

-- Max. 50 MB und nur gängige Videoformate (Supabase prüft das beim Upload)
update storage.buckets
set file_size_limit = 52428800,
    allowed_mime_types = array['video/mp4', 'video/webm', 'video/quicktime']
where id = 'reels';

update storage.buckets
set file_size_limit = 10485760,
    allowed_mime_types = array['image/jpeg', 'image/png', 'image/webp']
where id in ('stories', 'avatars', 'post-images');

drop policy if exists "reels_public_read" on storage.objects;
create policy "reels_public_read" on storage.objects
    for select using (bucket_id = 'reels');

drop policy if exists "reels_insert_own_folder" on storage.objects;
create policy "reels_insert_own_folder" on storage.objects
    for insert to authenticated
    with check (bucket_id = 'reels' and (storage.foldername(name))[1] = auth.uid()::text);

drop policy if exists "reels_delete_own_folder" on storage.objects;
create policy "reels_delete_own_folder" on storage.objects
    for delete to authenticated
    using (bucket_id = 'reels' and (storage.foldername(name))[1] = auth.uid()::text);

commit;
