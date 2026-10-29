-- 009_views_and_functions.sql
-- Lese-Views und RPC-Funktionen für die App

-- Feed: Beiträge inkl. Autor, Bilder und Like-/Bookmark-Status des aktuellen Users.
-- security_invoker = true, damit die RLS-Policies der Basistabellen greifen.
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
    exists (select 1 from public.bookmarks b where b.post_id = p.id and b.user_id = auth.uid()) as is_bookmarked
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
